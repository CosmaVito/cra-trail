using System.Net;
using System.Net.Http.Json;
using CvdInbox.Data;
using CvdInbox.Inbox;
using CvdInbox.Operators;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CvdInbox.Tests;

public class InboxEndpointTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Anonymous_requests_get_401()
    {
        await using var app = new InboxApp();

        var response = await app.CreateBrowser().GetAsync("/api/inbox", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lists_reports_newest_first_and_filters_by_status()
    {
        await using var app = new InboxApp();
        var now = app.Time.GetUtcNow().UtcDateTime;
        var oldest = await app.AddReportAsync(TestData.Report(now.AddDays(-2)));
        var triaged = await app.AddReportAsync(TestData.Report(now.AddDays(-1), status: ReportStatus.Triage));
        var newest = await app.AddReportAsync(TestData.Report(now));
        var client = await Browser.SignedInAsync(app);

        var all = await client.GetFromJsonAsync<List<ReportSummary>>("/api/inbox", TestJson.Options, Ct);
        var triage = await client.GetFromJsonAsync<List<ReportSummary>>("/api/inbox?status=triage", TestJson.Options, Ct);

        Assert.Equal(new[] { newest, triaged, oldest }, all!.Select(r => r.Id));
        Assert.Equal(new[] { triaged }, triage!.Select(r => r.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/inbox?status=bogus", Ct)).StatusCode);
    }

    [Fact]
    public async Task Shows_a_report_with_its_notes_and_next_statuses()
    {
        await using var app = new InboxApp();
        var id = await app.AddReportAsync(TestData.Report(app.Time.GetUtcNow().UtcDateTime));
        var client = await Browser.SignedInAsync(app);
        (await client.PostAsJsonAsync($"/api/inbox/{id}/notes", new { text = "Reproduced on 1.2.3." }, Ct)).EnsureSuccessStatusCode();

        var detail = await client.GetFromJsonAsync<ReportDetail>($"/api/inbox/{id}", TestJson.Options, Ct);

        Assert.Equal("Stored XSS in the comment field of the admin page.", detail!.Description);
        Assert.Equal("researcher@example.test", detail.Contact);
        Assert.Equal(new[] { ReportStatus.Triage, ReportStatus.Rejected }, detail.NextStatuses);
        var note = Assert.Single(detail.Notes);
        Assert.Equal(("alice", "Reproduced on 1.2.3."), (note.Author, note.Text));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/inbox/999", Ct)).StatusCode);
    }

    [Fact]
    public async Task Moves_a_report_along_the_workflow_and_records_who_did_it()
    {
        await using var app = new InboxApp();
        var id = await app.AddReportAsync(TestData.Report(app.Time.GetUtcNow().UtcDateTime));
        var client = await Browser.SignedInAsync(app);

        var moved = await client.PostAsJsonAsync($"/api/inbox/{id}/status", new { status = "triage" }, Ct);
        var skipped = await client.PostAsJsonAsync($"/api/inbox/{id}/status", new { status = "disclosed" }, Ct);

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var detail = await moved.Content.ReadFromJsonAsync<ReportDetail>(TestJson.Options, Ct);
        Assert.Equal(ReportStatus.Triage, detail!.Status);
        Assert.Equal(new[] { ReportStatus.Accepted, ReportStatus.Rejected }, detail.NextStatuses);
        Assert.Equal(HttpStatusCode.Conflict, skipped.StatusCode);
        var changed = Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1201);
        Assert.Contains("alice", changed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Closing_starts_the_retention_clock_and_reopening_stops_it()
    {
        await using var app = new InboxApp();
        var id = await app.AddReportAsync(TestData.Report(app.Time.GetUtcNow().UtcDateTime));
        var client = await Browser.SignedInAsync(app);

        var rejected = await (await client.PostAsJsonAsync($"/api/inbox/{id}/status", new { status = "rejected" }, Ct))
            .Content.ReadFromJsonAsync<ReportDetail>(TestJson.Options, Ct);
        var reopened = await (await client.PostAsJsonAsync($"/api/inbox/{id}/status", new { status = "triage" }, Ct))
            .Content.ReadFromJsonAsync<ReportDetail>(TestJson.Options, Ct);

        Assert.Equal(app.Time.GetUtcNow().UtcDateTime, rejected!.ClosedAt);
        Assert.Null(reopened!.ClosedAt);
    }

    [Fact]
    public async Task Notes_need_text_without_control_characters()
    {
        await using var app = new InboxApp();
        var id = await app.AddReportAsync(TestData.Report(app.Time.GetUtcNow().UtcDateTime));
        var client = await Browser.SignedInAsync(app);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/inbox/{id}/notes", new { text = "   " }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/inbox/{id}/notes", new { text = "fixed \u202E in 1.2.4" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/inbox/999/notes", new { text = "Hello" }, Ct)).StatusCode);

        var added = await client.PostAsJsonAsync($"/api/inbox/{id}/notes", new { text = "Fixed in 1.2.4." }, Ct);

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var note = await added.Content.ReadFromJsonAsync<NoteView>(TestJson.Options, Ct);
        Assert.Equal("alice", note!.Author);
        Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1202);
    }

    [Fact]
    public async Task Changes_need_the_request_token()
    {
        await using var app = new InboxApp();
        var id = await app.AddReportAsync(TestData.Report(app.Time.GetUtcNow().UtcDateTime));
        var client = await Browser.SignedInAsync(app);
        client.DefaultRequestHeaders.Remove(OperatorSignIn.TokenHeader);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/inbox/{id}/status", new { status = "triage" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/inbox/{id}", Ct)).StatusCode);
        // Reading needs no token.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/inbox/{id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_report_leaves_no_trace_in_the_database_files()
    {
        await using var app = new InboxApp();
        var report = TestData.Report(app.Time.GetUtcNow().UtcDateTime, contact: "zz-contact-marker-4711@example.test");
        report.Description = "zz-description-marker-4711 with enough words to pass";
        var id = await app.AddReportAsync(report);
        var client = await Browser.SignedInAsync(app);
        (await client.PostAsJsonAsync($"/api/inbox/{id}/notes", new { text = "zz-note-marker-4711" }, Ct)).EnsureSuccessStatusCode();
        // The check can see the data before the deletion.
        Assert.True(DataFiles.Contain(app.DataDirectory, "zz-note-marker-4711"));

        var deleted = await client.DeleteAsync($"/api/inbox/{id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/inbox/{id}", Ct)).StatusCode);
        Assert.Equal(0, await app.WithDbAsync(db => db.Notes.CountAsync()));
        foreach (var marker in new[] { "zz-description-marker-4711", "zz-contact-marker-4711", "zz-note-marker-4711" })
            Assert.False(DataFiles.Contain(app.DataDirectory, marker), marker);
        Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1203);
    }

    [Fact]
    public async Task A_reader_that_stays_open_delays_the_log_truncation_and_it_is_logged_then_retried()
    {
        await using var app = new InboxApp();
        var report = TestData.Report(app.Time.GetUtcNow().UtcDateTime, contact: "zz-contact-marker-4711@example.test");
        var id = await app.AddReportAsync(report);
        var client = await Browser.SignedInAsync(app);

        // An open read transaction pins the log, as a backup or a slow query would.
        await using (var reader = new SqliteConnection($"Data Source={Path.Combine(app.DataDirectory, DataSetup.DatabaseFile)}"))
        {
            await reader.OpenAsync(Ct);
            await using (var begin = reader.CreateCommand())
            {
                begin.CommandText = "BEGIN; SELECT COUNT(*) FROM Reports;";
                await begin.ExecuteScalarAsync(Ct);
            }

            var deleted = await client.DeleteAsync($"/api/inbox/{id}", Ct);

            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            var warning = Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1204);
            Assert.DoesNotContain("zz-contact", warning.Message, StringComparison.Ordinal);
            Assert.True(DataFiles.Contain(app.DataDirectory, "zz-contact-marker-4711"));
        }

        Assert.True(await app.WithDbAsync(db => db.TruncateWalAsync(Ct)));
        Assert.False(DataFiles.Contain(app.DataDirectory, "zz-contact-marker-4711"));
    }
}

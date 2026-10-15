using System.Net.Http.Json;
using CvdInbox.Data;
using CvdInbox.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CvdInbox.Tests;

public class RetentionTests
{
    [Fact]
    public async Task Contacts_of_open_reports_stay()
    {
        await using var app = App();
        var id = await app.AddReportAsync(TestData.Report(Now(app).AddDays(-400), status: ReportStatus.Triage));

        await SweepAsync(app);

        Assert.NotNull((await FindAsync(app, id)).Contact);
    }

    [Fact]
    public async Task Contacts_go_30_days_after_the_report_closes()
    {
        await using var app = App();
        var id = await app.AddReportAsync(Closed(Now(app).AddDays(-30).AddHours(1)));

        Assert.Equal(0, await SweepAsync(app));
        app.Time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(1, await SweepAsync(app));

        var report = await FindAsync(app, id);
        Assert.Null(report.Contact);
        Assert.Equal(Now(app), report.ContactErasedAt);
        Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1301);
    }

    [Fact]
    public async Task The_retention_period_comes_from_the_configuration()
    {
        await using var app = App(("Inbox:ContactRetentionDays", "0"));
        var id = await app.AddReportAsync(Closed(Now(app)));

        await SweepAsync(app);

        Assert.Null((await FindAsync(app, id)).Contact);
    }

    [Fact]
    public async Task Reopening_a_report_stops_the_clock()
    {
        await using var app = App();
        var id = await app.AddReportAsync(TestData.Report(Now(app)));
        var client = await Browser.SignedInAsync(app);
        (await client.PostAsJsonAsync($"/api/inbox/{id}/status", new { status = "rejected" }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        app.Time.Advance(TimeSpan.FromDays(20));
        // The session cookie expired with the clock; sign in again.
        client = await Browser.SignedInAsync(app);
        (await client.PostAsJsonAsync($"/api/inbox/{id}/status", new { status = "triage" }, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        app.Time.Advance(TimeSpan.FromDays(20));
        await SweepAsync(app);

        Assert.NotNull((await FindAsync(app, id)).Contact);
    }

    [Fact]
    public async Task Erased_contacts_leave_no_bytes_in_the_database_files()
    {
        await using var app = App();
        var report = Closed(Now(app).AddDays(-31));
        report.Contact = "zz-contact-marker-0815@example.test";
        await app.AddReportAsync(report);
        // The check can see the contact before the erasure.
        Assert.True(DataFiles.Contain(app.DataDirectory, "zz-contact-marker-0815"));

        await SweepAsync(app);

        Assert.False(DataFiles.Contain(app.DataDirectory, "zz-contact-marker-0815"));
        // Another test clearing the connection pools can checkpoint the log too; the bytes alone would hide a failed truncate.
        Assert.DoesNotContain(app.SecurityEvents(), e => e.Id.Id == 1204);
    }

    [Fact]
    public async Task The_background_sweep_erases_expired_contacts()
    {
        await using var app = new InboxApp(retentionSweep: true);
        var start = Now(app);
        // Neither contact has expired at startup, and the clock moves on only once the first is gone,
        // so two sweeps erase them at two different times: the timer ticked at least once.
        var first = await app.AddReportAsync(Closed(start.AddDays(-30).AddMinutes(30)));
        var second = await app.AddReportAsync(Closed(start.AddDays(-30).AddMinutes(90)));

        app.Time.Advance(TimeSpan.FromHours(1));
        await WaitForErasureAsync(app, first);
        app.Time.Advance(TimeSpan.FromHours(1));
        await WaitForErasureAsync(app, second);

        Assert.Equal(start.AddHours(1), (await FindAsync(app, first)).ContactErasedAt);
        Assert.Equal(start.AddHours(2), (await FindAsync(app, second)).ContactErasedAt);
    }

    private static InboxApp App(params (string Key, string Value)[] extra) =>
        new(extra.ToDictionary(setting => setting.Key, setting => (string?)setting.Value));

    private static DateTime Now(InboxApp app) => app.Time.GetUtcNow().UtcDateTime;

    private static Report Closed(DateTime closedAt)
    {
        var report = TestData.Report(closedAt.AddDays(-1), status: ReportStatus.Rejected);
        report.ClosedAt = closedAt;
        return report;
    }

    // One sweep of the background service, without its timer: erase, then truncate the log.
    private static Task<int> SweepAsync(InboxApp app) => app.InScopeAsync(async services =>
    {
        var eraser = services.GetRequiredService<ContactEraser>();
        var erased = await eraser.EraseExpiredAsync(CancellationToken.None);
        await eraser.TruncateWalAsync(CancellationToken.None);
        return erased;
    });

    // The sweep runs on a background thread; give it a moment after the clock moves.
    private static async Task WaitForErasureAsync(InboxApp app, int id)
    {
        for (var attempt = 0; attempt < 100 && (await FindAsync(app, id)).Contact is not null; attempt++)
            await Task.Delay(100, TestContext.Current.CancellationToken);
    }

    private static Task<Report> FindAsync(InboxApp app, int id) =>
        app.WithDbAsync(db => db.Reports.AsNoTracking().SingleAsync(r => r.Id == id));
}

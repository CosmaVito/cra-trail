using System.Net.Http.Json;
using CvdInbox.Data;
using CvdInbox.Retention;
using CvdInbox.Security;
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

    [Fact]
    public async Task A_failed_erasure_is_logged_and_the_truncate_still_runs()
    {
        await using var app = App();
        var truncated = false;

        await RetentionService.SweepAsync(
            () => throw new InvalidOperationException("disk full"),
            () =>
            {
                truncated = true;
                return Task.CompletedTask;
            },
            app.Services.GetRequiredService<SecurityEvents>(), TestContext.Current.CancellationToken);

        Assert.True(truncated);
        var failed = Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1302);
        Assert.Equal("disk full", failed.Exception?.Message);
    }

    [Fact]
    public async Task The_sweep_erases_first_then_truncates_and_logs_a_failed_truncate()
    {
        await using var app = App();
        var events = app.Services.GetRequiredService<SecurityEvents>();

        await RetentionService.SweepAsync(
            () =>
            {
                events.ContactsErased(2);
                return Task.CompletedTask;
            },
            () => Task.FromException(new IOException("locked")),
            events, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { 1301, 1303 }, app.SecurityEvents().Select(e => e.Id.Id).Where(id => id is 1301 or 1303));
    }

    [Fact]
    public async Task Shutdown_ends_the_sweep_without_an_error_event()
    {
        await using var app = App();
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RetentionService.SweepAsync(
            () => Task.FromCanceled(stopping.Token), () => Task.CompletedTask, app.Services.GetRequiredService<SecurityEvents>(), stopping.Token));

        Assert.DoesNotContain(app.SecurityEvents(), e => e.Id.Id is 1302 or 1303);
    }

    [Fact]
    public async Task Other_failures_during_shutdown_are_still_logged()
    {
        await using var app = App();
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();

        await RetentionService.SweepAsync(
            () => throw new InvalidOperationException("closed"), () => Task.CompletedTask, app.Services.GetRequiredService<SecurityEvents>(), stopping.Token);

        Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1302);
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
    // One sweep of the background service, with its real steps and without its timer.
    private static Task<int> SweepAsync(InboxApp app) => app.InScopeAsync(async services =>
    {
        var eraser = services.GetRequiredService<ContactEraser>();
        var erased = 0;
        await RetentionService.SweepAsync(
            async () => erased = await eraser.EraseExpiredAsync(CancellationToken.None),
            () => eraser.TruncateWalAsync(CancellationToken.None),
            services.GetRequiredService<SecurityEvents>(), CancellationToken.None);
        return erased;
    });

    // The sweep runs on a background thread; give it up to 10 seconds after the clock moves.
    private static async Task WaitForErasureAsync(InboxApp app, int id)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if ((await FindAsync(app, id)).Contact is null)
                return;
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        Assert.Fail($"The contact of report {id} was still there after 10 seconds.");
    }

    private static Task<Report> FindAsync(InboxApp app, int id) =>
        app.WithDbAsync(db => db.Reports.AsNoTracking().SingleAsync(r => r.Id == id));
}

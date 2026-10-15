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

        await EraseAsync(app);

        Assert.NotNull((await FindAsync(app, id)).Contact);
    }

    [Fact]
    public async Task Contacts_go_30_days_after_the_report_closes()
    {
        await using var app = App();
        var id = await app.AddReportAsync(Closed(Now(app).AddDays(-30).AddHours(1)));

        Assert.Equal(0, await EraseAsync(app));
        app.Time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(1, await EraseAsync(app));

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

        await EraseAsync(app);

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
        await EraseAsync(app);

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

        await EraseAsync(app);

        Assert.False(DataFiles.Contain(app.DataDirectory, "zz-contact-marker-0815"));
    }

    [Fact]
    public async Task The_background_sweep_erases_expired_contacts()
    {
        await using var app = new InboxApp();
        var id = await app.AddReportAsync(Closed(Now(app).AddDays(-31)));

        // The sweep runs every hour of the fake clock; give the background service a moment after each tick.
        for (var attempt = 0; attempt < 50 && (await FindAsync(app, id)).Contact is not null; attempt++)
        {
            app.Time.Advance(TimeSpan.FromHours(1));
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.Null((await FindAsync(app, id)).Contact);
    }

    // A one-day sweep and no background service keep the sweep out of the way while a test moves the clock by hours.
    private static InboxApp App(params (string Key, string Value)[] extra)
    {
        var settings = new Dictionary<string, string?> { ["Inbox:RetentionSweepInterval"] = "1.00:00:00" };
        foreach (var (key, value) in extra)
            settings[key] = value;
        return new InboxApp(settings, retentionSweep: false);
    }

    private static DateTime Now(InboxApp app) => app.Time.GetUtcNow().UtcDateTime;

    private static Report Closed(DateTime closedAt)
    {
        var report = TestData.Report(closedAt.AddDays(-1), status: ReportStatus.Rejected);
        report.ClosedAt = closedAt;
        return report;
    }

    private static Task<int> EraseAsync(InboxApp app) =>
        app.InScopeAsync(services => services.GetRequiredService<ContactEraser>().EraseExpiredAsync(CancellationToken.None));

    private static Task<Report> FindAsync(InboxApp app, int id) =>
        app.WithDbAsync(db => db.Reports.AsNoTracking().SingleAsync(r => r.Id == id));
}

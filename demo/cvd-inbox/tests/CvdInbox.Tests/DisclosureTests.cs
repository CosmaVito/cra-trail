using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace CvdInbox.Tests;

public class DisclosureTests
{
    private const string Expected =
        "Contact: https://inbox.example.test/\n" +
        "Contact: mailto:security@example.test\n" +
        "Expires: 2027-06-30T00:00:00Z\n" +
        "Preferred-Languages: en\n" +
        "Canonical: https://inbox.example.test/.well-known/security.txt\n" +
        "Policy: https://inbox.example.test/security-policy\n";

    [Fact]
    public async Task Security_txt_is_utf8_text_with_the_required_fields()
    {
        await using var app = new InboxApp();

        var response = await app.CreateBrowser().GetAsync("/.well-known/security.txt", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.Equal(Expected, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Canonical_ignores_the_host_header()
    {
        await using var app = new InboxApp();
        var client = app.CreateBrowser();
        client.DefaultRequestHeaders.Host = "attacker.example";

        var body = await client.GetStringAsync("/.well-known/security.txt", TestContext.Current.CancellationToken);

        Assert.Equal(Expected, body);
    }

    [Theory]
    [InlineData("Disclosure:PublicOrigin", "http://inbox.example.test", "Disclosure:PublicOrigin")]
    [InlineData("Disclosure:PublicOrigin", "https://inbox.example.test/inbox", "Disclosure:PublicOrigin")]
    [InlineData("Disclosure:PublicOrigin", null, "Disclosure:PublicOrigin")]
    [InlineData("Disclosure:Expires", "2026-10-14T00:00:00Z", "Disclosure:Expires")]
    [InlineData("Disclosure:Expires", null, "Disclosure:Expires")]
    [InlineData("Disclosure:Contact:0", "ftp://files.example.test", "Disclosure:Contact")]
    [InlineData("Disclosure:Contact:0", "mailto:security@example.test\nPolicy: https://evil.example", "Disclosure:Contact")]
    [InlineData("Disclosure:PreferredLanguages", "en\nContact: mailto:evil@example.test", "Disclosure:PreferredLanguages")]
    public async Task Startup_refuses_settings_that_would_publish_a_wrong_security_txt(string key, string? value, string named)
    {
        await using var app = new InboxApp(new Dictionary<string, string?> { [key] = value });

        var error = app.StartupError();

        Assert.NotNull(error);
        Assert.Contains(named, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_expiry_within_30_days_is_logged_as_a_warning()
    {
        await using var app = new InboxApp(new Dictionary<string, string?> { ["Disclosure:Expires"] = "2026-11-01T09:00:00Z" });

        _ = app.CreateBrowser();

        var warning = await WaitForEventAsync(app, 1401);
        Assert.Equal("security.txt expires on 2026-11-01; set Disclosure:Expires to a later date and restart.", warning.Message);
    }

    [Fact]
    public async Task A_server_running_past_the_expiry_logs_an_error_the_next_day()
    {
        await using var app = new InboxApp(new Dictionary<string, string?> { ["Disclosure:Expires"] = "2026-10-16T00:00:00Z" });
        _ = app.CreateBrowser();
        await WaitForEventAsync(app, 1401);

        app.Time.Advance(TimeSpan.FromDays(1));

        var expired = await WaitForEventAsync(app, 1402);
        Assert.Equal("security.txt expired on 2026-10-16; set Disclosure:Expires to a later date and restart.", expired.Message);
        Assert.Equal(LogLevel.Error, expired.Level);
    }

    [Fact]
    public async Task The_policy_page_data_comes_from_the_configuration()
    {
        await using var app = new InboxApp();

        var policy = await app.CreateBrowser().GetFromJsonAsync<JsonElement>("/api/disclosure", TestContext.Current.CancellationToken);

        Assert.Equal("https://inbox.example.test/", policy.GetProperty("reportUrl").GetString());
        Assert.Equal("mailto:security@example.test", policy.GetProperty("contact")[0].GetString());
        Assert.Equal(5, policy.GetProperty("acknowledgeWithinDays").GetInt32());
        Assert.Equal(90, policy.GetProperty("disclosureAfterDays").GetInt32());
        Assert.Equal(30, policy.GetProperty("contactRetentionDays").GetInt32());
    }

    // The check runs on a background thread at start and then once a day.
    private static async Task<FakeLogRecord> WaitForEventAsync(InboxApp app, int id)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (app.SecurityEvents().FirstOrDefault(e => e.Id.Id == id) is { } record)
                return record;
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        Assert.Fail($"No event {id} after 5 seconds.");
        return null!;
    }
}

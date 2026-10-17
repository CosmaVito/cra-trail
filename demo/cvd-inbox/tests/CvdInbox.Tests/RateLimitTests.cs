using System.Net;
using System.Net.Http.Json;

namespace CvdInbox.Tests;

public class RateLimitTests
{
    [Fact]
    public async Task Reports_over_the_limit_get_429_with_a_retry_after()
    {
        await using var app = App();
        var client = app.CreateBrowser(peer: "198.51.100.1");
        await TestData.SubmitAsync(client);
        await TestData.SubmitAsync(client);

        var response = await client.PostAsJsonAsync("/api/reports", TestData.NewReport, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        var limited = Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1103);
        Assert.Contains("reports", limited.Message, StringComparison.Ordinal);
        Assert.Contains("198.51.100.1", limited.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_client_address_has_its_own_window()
    {
        await using var app = App();
        var first = app.CreateBrowser(peer: "198.51.100.1");
        await TestData.SubmitAsync(first);
        await TestData.SubmitAsync(first);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await first.PostAsJsonAsync("/api/reports", TestData.NewReport, TestContext.Current.CancellationToken)).StatusCode);

        var second = app.CreateBrowser(peer: "198.51.100.2");

        Assert.Equal(HttpStatusCode.Created, (await second.PostAsJsonAsync("/api/reports", TestData.NewReport, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Forwarded_for_counts_only_when_a_trusted_proxy_sends_it()
    {
        await using var app = App(("Inbox:TrustedProxies:0", "10.0.0.1"));

        // A direct client cannot get a fresh window by inventing a new address each time.
        var direct = app.CreateBrowser(peer: "10.0.0.9");
        var directAnswers = new List<HttpStatusCode>();
        foreach (var forged in new[] { "192.0.2.1", "192.0.2.2", "192.0.2.3" })
            directAnswers.Add(await PostAsync(direct, forged));
        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.TooManyRequests }, directAnswers);

        // Behind the trusted proxy, every client has its own window.
        var proxy = app.CreateBrowser(peer: "10.0.0.1");
        Assert.Equal(HttpStatusCode.Created, await PostAsync(proxy, "192.0.2.10"));
        Assert.Equal(HttpStatusCode.Created, await PostAsync(proxy, "192.0.2.10"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await PostAsync(proxy, "192.0.2.10"));
        Assert.Equal(HttpStatusCode.Created, await PostAsync(proxy, "192.0.2.11"));
    }

    [Fact]
    public async Task Ipv6_clients_share_a_window_per_64_network()
    {
        await using var app = App();
        await TestData.SubmitAsync(app.CreateBrowser(peer: "2001:db8:1:2::1"));
        await TestData.SubmitAsync(app.CreateBrowser(peer: "2001:db8:1:2::2"));

        // One subscriber usually gets a whole /64 and can pick any address in it.
        Assert.Equal(HttpStatusCode.TooManyRequests, await PostAsync(app.CreateBrowser(peer: "2001:db8:1:2:ffff::3")));
        Assert.Equal(HttpStatusCode.Created, await PostAsync(app.CreateBrowser(peer: "2001:db8:1:3::1")));
        var limited = Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1103);
        Assert.Contains("2001:db8:1:2:ffff::3", limited.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_ipv4_mapped_address_is_the_same_client_as_its_ipv4_address()
    {
        await using var app = App();
        await TestData.SubmitAsync(app.CreateBrowser(peer: "::ffff:198.51.100.20"));
        await TestData.SubmitAsync(app.CreateBrowser(peer: "198.51.100.20"));

        Assert.Equal(HttpStatusCode.TooManyRequests, await PostAsync(app.CreateBrowser(peer: "::ffff:198.51.100.20")));
        var limited = Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1103);
        Assert.Equal("Rate limit reports reached by 198.51.100.20.", limited.Message);
    }

    [Fact]
    public async Task A_client_that_keeps_hitting_the_limit_is_logged_once_a_minute()
    {
        await using var app = App();
        var client = app.CreateBrowser(peer: "198.51.100.21");
        await TestData.SubmitAsync(client);
        await TestData.SubmitAsync(client);

        for (var attempt = 0; attempt < 3; attempt++)
            Assert.Equal(HttpStatusCode.TooManyRequests, await PostAsync(client));

        Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1103);
    }

    [Theory]
    [InlineData("127.0.0.1", "10.0.0.1")]
    [InlineData("10.0.0.1", null)]
    public async Task Forged_forwarded_for_headers_do_not_open_new_windows(string peer, string? trustedProxy)
    {
        await using var app = trustedProxy is null ? App() : App(("Inbox:TrustedProxies:0", trustedProxy));
        var client = app.CreateBrowser(peer: peer);

        var answers = new List<HttpStatusCode>();
        foreach (var forged in new[] { "192.0.2.1", "192.0.2.2", "192.0.2.3" })
            answers.Add(await PostAsync(client, forged));

        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.TooManyRequests }, answers);
    }

    [Fact]
    public async Task Behind_one_trusted_proxy_the_client_is_the_last_forwarded_address()
    {
        await using var app = App(("Inbox:TrustedProxies:0", "10.0.0.1"));
        var proxy = app.CreateBrowser(peer: "10.0.0.1");

        // One proxy hop: the proxy appends the address it saw, and anything before it came from the client.
        Assert.Equal(HttpStatusCode.Created, await PostAsync(proxy, "192.0.2.50, 192.0.2.60"));
        Assert.Equal(HttpStatusCode.Created, await PostAsync(proxy, "192.0.2.60"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await PostAsync(proxy, "192.0.2.51, 192.0.2.60"));

        var limited = Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1103);
        Assert.Equal("Rate limit reports reached by 192.0.2.60.", limited.Message);
    }

    [Fact]
    public async Task Status_lookups_have_their_own_limit()
    {
        await using var app = App();
        var client = app.CreateBrowser(peer: "198.51.100.3");
        var receipt = await TestData.SubmitAsync(client);

        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/reports/status", new { receipt }, TestContext.Current.CancellationToken)).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/reports/status", new { receipt }, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task A_trusted_proxy_must_be_an_ip_address()
    {
        await using var app = App(("Inbox:TrustedProxies:0", "proxy.internal"));

        var error = app.StartupError();

        Assert.NotNull(error);
        Assert.Contains("Inbox:TrustedProxies", error.ToString(), StringComparison.Ordinal);
    }

    private static InboxApp App(params (string Key, string Value)[] extra)
    {
        var settings = new Dictionary<string, string?>
        {
            ["RateLimits:Reports:PermitLimit"] = "2",
            ["RateLimits:Lookups:PermitLimit"] = "2",
        };
        foreach (var (key, value) in extra)
            settings[key] = value;
        return new InboxApp(settings);
    }

    private static async Task<HttpStatusCode> PostAsync(HttpClient client, string? forwardedFor = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reports") { Content = JsonContent.Create(TestData.NewReport) };
        if (forwardedFor is not null)
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        return (await client.SendAsync(request)).StatusCode;
    }
}

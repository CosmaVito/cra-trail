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

        var error = Record.Exception(() => app.CreateBrowser());

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

    private static async Task<HttpStatusCode> PostAsync(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reports") { Content = JsonContent.Create(TestData.NewReport) };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return (await client.SendAsync(request)).StatusCode;
    }
}

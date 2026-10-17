using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CvdInbox.Data;
using CvdInbox.Operators;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CvdInbox.Tests;

public class SignInTests
{
    private const string WrongPassword = "wrong password, long enough";
    private const string TrustedProxy = "10.0.0.1";

    [Fact]
    public async Task A_failed_sign_in_is_counted_even_when_the_client_leaves_during_the_check()
    {
        var hasher = new GateHasher();
        await using var app = new InboxApp(testServices: services => services.AddSingleton<IPasswordHasher<Operator>>(hasher));
        await app.CreateOperatorAsync();
        var client = app.CreateBrowser();
        await Browser.RefreshTokenAsync(client);
        using var leave = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var signIn = client.PostAsJsonAsync("/api/auth/sign-in", new { name = "alice", password = WrongPassword }, leave.Token);
        await hasher.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await leave.CancelAsync();
        hasher.Released.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signIn);

        // The server finishes the request after the client has gone.
        for (var waited = 0; waited < 100 && !app.SecurityEvents().Any(e => e.Id.Id == 1002); waited++)
            await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1002);
        Assert.Equal(1, await app.WithDbAsync(db => db.Operators.Select(o => o.FailedSignIns).SingleAsync()));
    }

    [Fact]
    public async Task Signing_in_sets_a_host_only_secure_strict_session_cookie()
    {
        await using var app = App();
        await app.CreateOperatorAsync();
        var client = app.CreateBrowser();
        await Browser.RefreshTokenAsync(client);

        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { name = "alice", password = InboxApp.AlicePassword }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith(OperatorSignIn.SessionCookie + "=", StringComparison.Ordinal))
            .ToLowerInvariant();
        Assert.Contains("; secure", cookie, StringComparison.Ordinal);
        Assert.Contains("; httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("; samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("; path=/", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("domain=", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("expires=", cookie, StringComparison.Ordinal);
        var session = await client.GetFromJsonAsync<SessionView>("/api/auth/session", TestJson.Options, TestContext.Current.CancellationToken);
        Assert.Equal("alice", session!.Operator);
        var signedIn = Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1001);
        Assert.Contains("alice", signedIn.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cookies_are_secure_even_over_plain_http()
    {
        await using var app = App();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/auth/session", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookies = response.Headers.GetValues("Set-Cookie").Select(c => c.ToLowerInvariant()).ToList();
        Assert.Equal(
            new[] { OperatorSignIn.AntiforgeryCookie.ToLowerInvariant(), OperatorSignIn.TokenCookie.ToLowerInvariant() },
            cookies.Select(c => c[..c.IndexOf('=', StringComparison.Ordinal)]).Order(StringComparer.Ordinal).ToArray());
        Assert.All(cookies, cookie => Assert.Contains("; secure", cookie, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Signing_in_needs_the_request_token()
    {
        await using var app = App();
        await app.CreateOperatorAsync();

        var response = await app.CreateBrowser(peer: "198.51.100.4")
            .PostAsJsonAsync("/api/auth/sign-in", new { name = "alice", password = InboxApp.AlicePassword }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var rejected = Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1104);
        Assert.Contains("198.51.100.4", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_token_from_before_sign_in_stops_working_after_it()
    {
        await using var app = App();
        await app.CreateOperatorAsync();
        var client = app.CreateBrowser();
        await Browser.RefreshTokenAsync(client);
        await client.PostAsJsonAsync("/api/auth/sign-in", new { name = "alice", password = InboxApp.AlicePassword }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/sign-out", null, TestContext.Current.CancellationToken)).StatusCode);

        await Browser.RefreshTokenAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/sign-out", null, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Unknown_names_wrong_passwords_and_locked_accounts_get_the_same_answer()
    {
        await using var app = App();
        await app.CreateOperatorAsync();
        var client = app.CreateBrowser();
        await Browser.RefreshTokenAsync(client);

        var unknown = await SignInAsync(client, "nobody", InboxApp.AlicePassword);
        var wrong = await SignInAsync(client, "alice", WrongPassword);
        for (var attempt = 0; attempt < 4; attempt++)
            await SignInAsync(client, "alice", WrongPassword);
        var locked = await SignInAsync(client, "alice", InboxApp.AlicePassword);

        Assert.All(new[] { unknown, wrong, locked }, answer =>
            Assert.Equal((HttpStatusCode.Unauthorized, "Wrong name or password."), answer));
        Assert.Contains(app.SecurityEvents(), e => e.Id.Id == 1003);
    }

    [Fact]
    public async Task Failed_sign_ins_are_recorded_without_the_password()
    {
        await using var app = App();
        await app.CreateOperatorAsync();
        var client = app.CreateBrowser(peer: "198.51.100.5");
        await Browser.RefreshTokenAsync(client);

        await SignInAsync(client, "alice", "my secret guess, long");
        // Shaped like a name, so only the lookup of existing operators keeps it out of the log.
        await SignInAsync(client, "my.secret.typed.as.a.name", "x");

        var failures = app.SecurityEvents().Where(e => e.Id.Id == 1002).Select(e => e.Message).ToList();
        Assert.Equal(2, failures.Count);
        Assert.Contains("alice", failures[0], StringComparison.Ordinal);
        Assert.Contains("198.51.100.5", failures[0], StringComparison.Ordinal);
        Assert.Contains("(unknown name)", failures[1], StringComparison.Ordinal);
        Assert.Contains("198.51.100.5", failures[1], StringComparison.Ordinal);
        Assert.DoesNotContain(app.Logs(), record => record.Message.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Signing_out_ends_the_session()
    {
        await using var app = App();
        var client = await Browser.SignedInAsync(app);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/sign-out", null, TestContext.Current.CancellationToken)).StatusCode);

        var session = await client.GetFromJsonAsync<SessionView>("/api/auth/session", TestJson.Options, TestContext.Current.CancellationToken);
        Assert.Null(session!.Operator);
        Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1004);
    }

    [Fact]
    public async Task Sign_in_attempts_are_rate_limited()
    {
        await using var app = new InboxApp(new Dictionary<string, string?> { ["RateLimits:SignIns:PermitLimit"] = "3" });
        var client = app.CreateBrowser();
        await Browser.RefreshTokenAsync(client);

        for (var attempt = 0; attempt < 3; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(client, "nobody", WrongPassword)).Status);

        var limited = await client.PostAsJsonAsync("/api/auth/sign-in", new { name = "nobody", password = WrongPassword }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Theory]
    [InlineData("http://inbox.example.test", TrustedProxy, "https")]
    [InlineData("http://localhost", "198.51.100.6", null)]
    [InlineData("http://127.0.0.1", "198.51.100.6", null)]
    [InlineData("http://[::1]", "198.51.100.6", null)]
    public async Task Sign_in_works_over_https_through_a_trusted_proxy_or_on_localhost(string origin, string peer, string? forwardedProto)
    {
        await using var app = new InboxApp(new Dictionary<string, string?> { ["Inbox:TrustedProxies:0"] = TrustedProxy });
        await app.CreateOperatorAsync();

        var response = await SignInOverPlainHttpAsync(app, origin, peer, forwardedProto);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(app.SecurityEvents(), e => e.Id.Id == 1001);
    }

    [Theory]
    [InlineData(TrustedProxy, null)]
    [InlineData("198.51.100.6", "https")]
    public async Task Sign_in_over_plain_http_to_a_public_host_is_refused(string peer, string? forwardedProto)
    {
        await using var app = new InboxApp(new Dictionary<string, string?> { ["Inbox:TrustedProxies:0"] = TrustedProxy });
        await app.CreateOperatorAsync();

        var response = await SignInOverPlainHttpAsync(app, "http://inbox.example.test", peer, forwardedProto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Operator sign-in needs HTTPS.", problem.GetProperty("title").GetString());
        Assert.DoesNotContain(app.SecurityEvents(), e => e.Id.Id is 1001 or 1002 or 1003);
    }

    private static InboxApp App() => new(new Dictionary<string, string?> { ["RateLimits:SignIns:PermitLimit"] = "50" });

    // Signs alice in with a valid request token over plain http, as a TLS proxy forwards it. Over http the test client
    // keeps Secure cookies to itself, so the cookies travel by hand.
    private static async Task<HttpResponseMessage> SignInOverPlainHttpAsync(InboxApp app, string origin, string peer, string? forwardedProto)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(origin), AllowAutoRedirect = false, HandleCookies = false });
        client.DefaultRequestHeaders.Add(TestPeerAddress.Header, peer);
        if (forwardedProto is not null)
            client.DefaultRequestHeaders.Add("X-Forwarded-Proto", forwardedProto);

        var session = await client.GetAsync("/api/auth/session");
        session.EnsureSuccessStatusCode();
        var cookies = session.Headers.GetValues("Set-Cookie").Select(c => c[..c.IndexOf(';', StringComparison.Ordinal)]).ToList();
        var token = cookies.Single(c => c.StartsWith(OperatorSignIn.TokenCookie + "=", StringComparison.Ordinal))[(OperatorSignIn.TokenCookie.Length + 1)..];

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in")
        {
            Content = JsonContent.Create(new { name = "alice", password = InboxApp.AlicePassword }),
        };
        request.Headers.Add("Cookie", string.Join("; ", cookies));
        request.Headers.Add(OperatorSignIn.TokenHeader, Uri.UnescapeDataString(token));
        return await client.SendAsync(request);
    }

    private static async Task<(HttpStatusCode Status, string? Title)> SignInAsync(HttpClient client, string name, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { name, password });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (response.StatusCode, problem.GetProperty("title").GetString());
    }
}

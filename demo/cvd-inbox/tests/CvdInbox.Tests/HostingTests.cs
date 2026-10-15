using System.Net;
using System.Text;
using CvdInbox.Hosting;

namespace CvdInbox.Tests;

public class HostingTests
{
    [Fact]
    public async Task Unknown_api_routes_get_a_problem_not_the_client_app()
    {
        await using var app = new InboxApp();

        var response = await app.CreateBrowser().GetAsync("/api/nothing-here", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Responses_carry_the_security_headers()
    {
        await using var app = new InboxApp();

        var response = await app.CreateBrowser().GetAsync("/api/nothing-here", TestContext.Current.CancellationToken);

        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, Header(response, "Content-Security-Policy"));
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Equal("no-store", Header(response, "Cache-Control"));
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task Bodies_over_128_KiB_are_refused()
    {
        await using var app = new InboxApp();
        using var content = new StringContent(new string('x', 200_000), Encoding.UTF8, "application/json");

        var response = await app.CreateBrowser().PostAsync("/api/nothing-here", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values)
        : response.Content.Headers.TryGetValues(name, out var contentValues) ? string.Join(",", contentValues)
        : "";
}

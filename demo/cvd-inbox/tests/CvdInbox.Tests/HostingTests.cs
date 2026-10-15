using System.Net;
using System.Text;
using CvdInbox.Hosting;
using Microsoft.AspNetCore.Hosting;

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

    [Fact]
    public async Task The_client_page_is_checked_again_on_every_visit_and_bundles_keep_their_caching()
    {
        var webRoot = Directory.CreateTempSubdirectory("cvd-inbox-web-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(webRoot.FullName, "index.html"), "<!doctype html><title>CVD Inbox</title>", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(webRoot.FullName, "main-ABCD1234.js"), "export {};", TestContext.Current.CancellationToken);
            await using var app = new InboxApp(new Dictionary<string, string?> { [WebHostDefaults.WebRootKey] = webRoot.FullName });
            var client = app.CreateBrowser();

            var bundle = await client.GetAsync("/main-ABCD1234.js", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, bundle.StatusCode);
            Assert.Null(bundle.Headers.CacheControl);
            // The fallback for client routes, the root and the file itself.
            foreach (var path in new[] { "/operator", "/", "/index.html" })
            {
                var page = await client.GetAsync(path, TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, page.StatusCode);
                Assert.Equal("no-cache", Header(page, "Cache-Control"));
            }
        }
        finally
        {
            webRoot.Delete(recursive: true);
        }
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values)
        : response.Content.Headers.TryGetValues(name, out var contentValues) ? string.Join(",", contentValues)
        : "";
}

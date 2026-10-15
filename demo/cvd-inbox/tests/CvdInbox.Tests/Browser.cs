using System.Net;
using System.Net.Http.Json;
using CvdInbox.Operators;

namespace CvdInbox.Tests;

// Does what the Angular client does: fetch the request token, send it back in the header.
internal static class Browser
{
    public static async Task RefreshTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/session");
        response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith(OperatorSignIn.TokenCookie + "=", StringComparison.Ordinal));
        var value = cookie[(OperatorSignIn.TokenCookie.Length + 1)..cookie.IndexOf(';', StringComparison.Ordinal)];
        client.DefaultRequestHeaders.Remove(OperatorSignIn.TokenHeader);
        client.DefaultRequestHeaders.Add(OperatorSignIn.TokenHeader, Uri.UnescapeDataString(value));
    }

    public static async Task<HttpClient> SignedInAsync(InboxApp app, string name = "alice", string password = InboxApp.AlicePassword)
    {
        await app.CreateOperatorAsync(name, password);
        var client = app.CreateBrowser();
        await RefreshTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { name, password });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        // The token is bound to the user, so the one from before signing in no longer works.
        await RefreshTokenAsync(client);
        return client;
    }
}

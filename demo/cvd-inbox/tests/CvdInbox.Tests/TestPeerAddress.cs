using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace CvdInbox.Tests;

// TestServer has no client address. This filter runs before the app's pipeline and sets it from a test header.
internal sealed class TestPeerAddress : IStartupFilter
{
    public const string Header = "X-Test-Peer";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(Header, out var peer))
                context.Connection.RemoteIpAddress = IPAddress.Parse(peer.ToString());
            return nextMiddleware(context);
        });
        next(app);
    };
}

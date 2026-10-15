using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace CvdInbox.Hosting;

public static class TrustedProxies
{
    // X-Forwarded-For is taken only from the proxies in Inbox:TrustedProxies. Anyone else could forge it.
    public static IApplicationBuilder UseTrustedProxies(this IApplicationBuilder app)
    {
        var proxies = app.ApplicationServices.GetRequiredService<IOptions<InboxOptions>>().Value.TrustedProxies;
        if (proxies.Count == 0)
            return app;

        var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var proxy in proxies)
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        return app.UseForwardedHeaders(options);
    }
}

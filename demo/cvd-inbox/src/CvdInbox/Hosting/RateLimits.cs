using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using CvdInbox.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CvdInbox.Hosting;

public sealed class WindowLimit
{
    public int PermitLimit { get; set; }

    public TimeSpan Window { get; set; }
}

public sealed class RateLimitOptions
{
    public const string Section = "RateLimits";

    public WindowLimit Reports { get; set; } = new() { PermitLimit = 5, Window = TimeSpan.FromHours(1) };

    public WindowLimit Lookups { get; set; } = new() { PermitLimit = 30, Window = TimeSpan.FromMinutes(10) };

    public WindowLimit SignIns { get; set; } = new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(15) };
}

public static class RateLimits
{
    public const string Reports = "reports";
    public const string Lookups = "lookups";
    public const string SignIns = "sign-ins";

    public static IServiceCollection AddInboxRateLimits(this IServiceCollection services)
    {
        services.AddOptions<RateLimitOptions>()
            .BindConfiguration(RateLimitOptions.Section)
            .Validate(o => new[] { o.Reports, o.Lookups, o.SignIns }.All(l => l.PermitLimit > 0 && l.Window > TimeSpan.Zero),
                "Every RateLimits entry needs a positive PermitLimit and Window.")
            .ValidateOnStart();

        services.AddSingleton<RejectionLog>();
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitOptions>>((limiter, limits) =>
        {
            // The default answer is 503, which tells the client nothing useful.
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(Reports, context => PerClient(context, limits.Value.Reports));
            limiter.AddPolicy(Lookups, context => PerClient(context, limits.Value.Lookups));
            limiter.AddPolicy(SignIns, context => PerClient(context, limits.Value.SignIns));
            limiter.OnRejected = (rejected, _) =>
            {
                var context = rejected.HttpContext;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                var policy = context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "unknown";
                if (context.RequestServices.GetRequiredService<RejectionLog>().ShouldLog(policy, ClientKey(context)))
                    context.RequestServices.GetRequiredService<SecurityEvents>().RateLimited(policy, SecurityEvents.ClientAddress(context));
                return ValueTask.CompletedTask;
            };
        });
        return services;
    }

    // One window per IPv4 address, or per /64 network for IPv6: a subscriber usually gets a whole /64 and can pick
    // any address in it. Behind a reverse proxy the address is right only when Inbox:TrustedProxies lists that proxy.
    public static string? ClientKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
            return null;
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();
        var bytes = address.GetAddressBytes();
        bytes.AsSpan(8).Clear();
        return new IPAddress(bytes) + "/64";
    }

    private static RateLimitPartition<string> PerClient(HttpContext context, WindowLimit limit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientKey(context) ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit.PermitLimit, Window = limit.Window, QueueLimit = 0 });
}

// Event 1103 at most once a minute per client and policy: a client that keeps hitting a limit would otherwise fill the log.
// One per app, not static, so parallel tests do not share it.
public sealed class RejectionLog : IDisposable
{
    private readonly PartitionedRateLimiter<string> limiter = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    public bool ShouldLog(string policy, string? clientKey)
    {
        using var lease = limiter.AttemptAcquire($"{policy} {clientKey}");
        return lease.IsAcquired;
    }

    public void Dispose() => limiter.Dispose();
}

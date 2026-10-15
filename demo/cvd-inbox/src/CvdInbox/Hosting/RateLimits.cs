using System.Globalization;
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
                context.RequestServices.GetRequiredService<SecurityEvents>().RateLimited(policy, SecurityEvents.ClientAddress(context));
                return ValueTask.CompletedTask;
            };
        });
        return services;
    }

    // One window per client address. Behind a reverse proxy the address is right only when
    // Inbox:TrustedProxies lists that proxy.
    private static RateLimitPartition<string> PerClient(HttpContext context, WindowLimit limit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            SecurityEvents.ClientAddress(context) ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit.PermitLimit, Window = limit.Window, QueueLimit = 0 });
}

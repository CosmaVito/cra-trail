using CvdInbox.Security;
using Microsoft.Extensions.Options;

namespace CvdInbox.Disclosure;

// The app refuses to start with an expired security.txt, but a server can run for months. This check warns every day
// from 30 days before the expiry, and reports a server that is still running after it.
public sealed class SecurityTxtExpiry(IOptions<DisclosureOptions> options, TimeProvider time, SecurityEvents events) : BackgroundService
{
    public static readonly TimeSpan WarningPeriod = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1), time);
        do
        {
            var expires = options.Value.Expires!.Value;
            var now = time.GetUtcNow();
            if (expires <= now)
                events.SecurityTxtExpired(expires);
            else if (expires - now <= WarningPeriod)
                events.SecurityTxtExpiresSoon(expires);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

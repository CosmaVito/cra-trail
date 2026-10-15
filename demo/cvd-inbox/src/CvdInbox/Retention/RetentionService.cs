using Microsoft.Extensions.Options;

namespace CvdInbox.Retention;

public sealed partial class RetentionService(
    IServiceScopeFactory scopes, TimeProvider time, IOptions<InboxOptions> options, ILogger<RetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.RetentionSweepInterval, time);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var eraser = scope.ServiceProvider.GetRequiredService<ContactEraser>();
                await eraser.EraseExpiredAsync(stoppingToken);
                // Every sweep, not only after an erasure: a truncate that missed after a delete is retried here.
                await eraser.TruncateWalAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                SweepFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(EventId = 1302, Level = LogLevel.Error, Message = "Erasing expired contacts failed; the next sweep tries again.")]
    private static partial void SweepFailed(ILogger logger, Exception exception);
}

public static class RetentionSetup
{
    public static IServiceCollection AddRetention(this IServiceCollection services)
    {
        services.AddScoped<ContactEraser>();
        services.AddHostedService<RetentionService>();
        return services;
    }
}

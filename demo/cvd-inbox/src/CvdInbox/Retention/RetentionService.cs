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
            await RunAsync(eraser => eraser.EraseExpiredAsync(stoppingToken), EraseFailed, stoppingToken);
            // Every sweep, also when the erasure failed: a truncate that missed after a delete is retried here.
            await RunAsync(eraser => eraser.TruncateWalAsync(stoppingToken), TruncateFailed, stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // A failed step is logged and the next sweep tries again; only shutdown ends the loop.
    private async Task RunAsync(Func<ContactEraser, Task> step, Action<ILogger, Exception> failed, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await step(scope.ServiceProvider.GetRequiredService<ContactEraser>());
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            failed(logger, exception);
        }
    }

    [LoggerMessage(EventId = 1302, Level = LogLevel.Error, Message = "Erasing expired contacts failed; the next sweep tries again.")]
    private static partial void EraseFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1303, Level = LogLevel.Error, Message = "Truncating the write-ahead log failed; the next sweep tries again.")]
    private static partial void TruncateFailed(ILogger logger, Exception exception);
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

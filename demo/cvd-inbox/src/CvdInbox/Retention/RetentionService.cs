using CvdInbox.Security;
using Microsoft.Extensions.Options;

namespace CvdInbox.Retention;

public sealed class RetentionService(
    IServiceScopeFactory scopes, TimeProvider time, IOptions<InboxOptions> options, SecurityEvents events) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.RetentionSweepInterval, time);
        do
        {
            await SweepAsync(
                () => InScopeAsync(eraser => eraser.EraseExpiredAsync(stoppingToken)),
                () => InScopeAsync(eraser => eraser.TruncateWalAsync(stoppingToken)),
                events,
                stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // Erase, then truncate the log. The truncate runs also when the erasure failed: a truncate that missed after a
    // delete is retried here. A failed step is logged and the next sweep tries again; only shutdown ends the loop.
    public static async Task SweepAsync(Func<Task> erase, Func<Task> truncate, SecurityEvents events, CancellationToken stoppingToken)
    {
        await RunAsync(erase, events.EraseFailed, stoppingToken);
        await RunAsync(truncate, events.TruncateFailed, stoppingToken);
    }

    private static async Task RunAsync(Func<Task> step, Action<Exception> failed, CancellationToken stoppingToken)
    {
        try
        {
            await step();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            failed(exception);
        }
    }

    // A scope per step, so a failed step leaves no state behind for the next one.
    private async Task InScopeAsync(Func<ContactEraser, Task> step)
    {
        await using var scope = scopes.CreateAsyncScope();
        await step(scope.ServiceProvider.GetRequiredService<ContactEraser>());
    }
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

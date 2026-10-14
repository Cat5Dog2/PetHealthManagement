using Microsoft.Extensions.Options;

namespace PetHealthManagement.Web.Services;

public class GuestAccountCleanupWorker(
    GuestAccountCleanupService cleanupService,
    GuestAccountCleanupQueue cleanupQueue,
    IOptions<GuestLoginOptions> options,
    TimeProvider timeProvider,
    ILogger<GuestAccountCleanupWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.CleanupEnabled)
        {
            logger.LogInformation("Expired guest cleanup is disabled.");
            return;
        }

        // DB 利用がない間は待機し、Azure SQL の自動休止を妨げない。
        await foreach (var _ in cleanupQueue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            finally
            {
                cleanupQueue.CompleteCleanup();
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            var result = await cleanupService.DeleteExpiredGuestsAsync(timeProvider.GetUtcNow(), stoppingToken);
            if (result.ExpiredCount > 0)
            {
                logger.LogInformation(
                    "Expired guest cleanup completed. expiredCount={ExpiredCount} deletedCount={DeletedCount} failedCount={FailedCount}",
                    result.ExpiredCount,
                    result.DeletedCount,
                    result.FailedCount);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // 次の DB 利用時に再試行する。失敗を契機に DB を定期的に起こさない。
            logger.LogError(ex, "Expired guest cleanup failed.");
        }
    }
}

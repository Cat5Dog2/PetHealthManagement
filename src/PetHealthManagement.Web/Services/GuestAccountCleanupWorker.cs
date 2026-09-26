using Microsoft.Extensions.Options;

namespace PetHealthManagement.Web.Services;

public class GuestAccountCleanupWorker(
    GuestAccountCleanupService cleanupService,
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

        // 起動時にも実行する。App Service Free F1 はアイドル時に停止するため、停止中に期限を迎えたゲストを次の起動で削除する。
        using var timer = new PeriodicTimer(Interval, timeProvider);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
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
            // DB に接続できない場合なども、アプリは止めずに次の周期で再試行する
            logger.LogError(ex, "Expired guest cleanup failed.");
        }
    }
}

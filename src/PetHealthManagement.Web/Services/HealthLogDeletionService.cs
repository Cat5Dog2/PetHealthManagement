using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public class HealthLogDeletionService(
    ApplicationDbContext dbContext,
    IImageStorageService imageStorageService,
    ILogger<HealthLogDeletionService> logger) : IHealthLogDeletionService
{
    public async Task DeleteAsync(
        HealthLog healthLog,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var owner = dbContext.Users.Local.FirstOrDefault(x => x.Id == ownerId)
            ?? await dbContext.Users.FirstOrDefaultAsync(x => x.Id == ownerId, cancellationToken);

        if (owner is null)
        {
            ApplicationOperationLogging.LogDeletionPreconditionFailed(
                logger,
                ApplicationOperationLogging.Operations.DeleteHealthLog,
                ownerId,
                "HealthLog",
                healthLog.Id,
                "owner_not_found");
            throw new InvalidOperationException("Owner user was not found.");
        }

        var healthLogImages = await dbContext.HealthLogImages
            .Include(x => x.Image)
            .Where(x => x.HealthLogId == healthLog.Id)
            .ToListAsync(cancellationToken);

        var imageAssets = healthLogImages
            .Select(x => x.Image)
            .Where(x => x is not null)
            .GroupBy(x => x.ImageId)
            .Select(x => x.First())
            .ToList();

        var storageTargets = imageAssets
            .Where(x => !string.IsNullOrWhiteSpace(x.StorageKey))
            .Select(x => new StorageDeletionTarget(x.ImageId, x.StorageKey, x.Category))
            .ToList();

        var deletedReadyBytes = imageAssets
            .Where(x => x.Status == ImageAssetStatus.Ready)
            .Sum(x => x.SizeBytes);

        ApplicationOperationLogging.LogDeletionStarted(
            logger,
            ApplicationOperationLogging.Operations.DeleteHealthLog,
            ownerId,
            "HealthLog",
            healthLog.Id,
            imageAssetCount: imageAssets.Count,
            storageTargetCount: storageTargets.Count,
            deletedReadyBytes: deletedReadyBytes);

        // 再試行でコールバックが再実行されても二重に減算しないよう、減算後の値は1回だけ求める
        var remainingUsedImageBytes = Math.Max(0, owner.UsedImageBytes - deletedReadyBytes);

        try
        {
            // EnableRetryOnFailure は実行戦略の外で開始したトランザクションを拒否するため、戦略の中で開始する。
            // 再試行ではコールバック全体が再実行されるので、中では DB 操作だけを行う。
            var executionStrategy = dbContext.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(
                async ct =>
                {
                    // コミット前に失敗した場合は、破棄（await using）でロールバックされる
                    await using var transaction = dbContext.Database.IsRelational()
                        ? await dbContext.Database.BeginTransactionAsync(ct)
                        : null;

                    if (healthLogImages.Count > 0)
                    {
                        dbContext.HealthLogImages.RemoveRange(healthLogImages);
                    }

                    if (imageAssets.Count > 0)
                    {
                        dbContext.ImageAssets.RemoveRange(imageAssets);
                    }

                    owner.UsedImageBytes = remainingUsedImageBytes;
                    dbContext.HealthLogs.Remove(healthLog);

                    // 変更の確定はコミット後に行う。コミットで失敗して再試行されても、同じ変更を送り直せるようにする。
                    await dbContext.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);

                    if (transaction is not null)
                    {
                        await transaction.CommitAsync(ct);
                    }
                },
                cancellationToken);

            dbContext.ChangeTracker.AcceptAllChanges();

            ApplicationOperationLogging.LogDeletionCompleted(
                logger,
                ApplicationOperationLogging.Operations.DeleteHealthLog,
                ownerId,
                "HealthLog",
                healthLog.Id,
                imageAssetCount: imageAssets.Count,
                storageTargetCount: storageTargets.Count,
                deletedReadyBytes: deletedReadyBytes);
        }
        catch (Exception ex)
        {
            ApplicationOperationLogging.LogDeletionFailed(
                logger,
                ex,
                ApplicationOperationLogging.Operations.DeleteHealthLog,
                ownerId,
                "HealthLog",
                healthLog.Id,
                imageAssetCount: imageAssets.Count,
                storageTargetCount: storageTargets.Count,
                deletedReadyBytes: deletedReadyBytes);

            throw;
        }

        await DeleteImageFilesBestEffortAsync(healthLog.Id, ownerId, storageTargets, cancellationToken);
    }

    private async Task DeleteImageFilesBestEffortAsync(
        int healthLogId,
        string ownerId,
        IReadOnlyList<StorageDeletionTarget> storageTargets,
        CancellationToken cancellationToken)
    {
        foreach (var target in storageTargets)
        {
            try
            {
                await imageStorageService.DeleteIfExistsAsync(target.StorageKey, cancellationToken);
            }
            catch (Exception ex)
            {
                ImageOperationLogging.LogDeleteFailed(
                    logger,
                    ex,
                    target.Category,
                    ownerId,
                    "HealthLog",
                    healthLogId,
                    ImageOperationLogging.Phases.CascadeDelete,
                    target.ImageId,
                    target.StorageKey);
            }
        }
    }

    private sealed record StorageDeletionTarget(Guid ImageId, string StorageKey, string Category);
}

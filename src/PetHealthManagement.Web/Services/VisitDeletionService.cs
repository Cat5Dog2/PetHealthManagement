using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public class VisitDeletionService(
    ApplicationDbContext dbContext,
    IImageStorageService imageStorageService,
    ILogger<VisitDeletionService> logger) : IVisitDeletionService
{
    public async Task DeleteAsync(
        Visit visit,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var owner = dbContext.Users.Local.FirstOrDefault(x => x.Id == ownerId)
            ?? await dbContext.Users.FirstOrDefaultAsync(x => x.Id == ownerId, cancellationToken);

        if (owner is null)
        {
            ApplicationOperationLogging.LogDeletionPreconditionFailed(
                logger,
                ApplicationOperationLogging.Operations.DeleteVisit,
                ownerId,
                "Visit",
                visit.Id,
                "owner_not_found");
            throw new InvalidOperationException("Owner user was not found.");
        }

        var visitImages = await dbContext.VisitImages
            .Include(x => x.Image)
            .Where(x => x.VisitId == visit.Id)
            .ToListAsync(cancellationToken);

        var imageAssets = visitImages
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
            ApplicationOperationLogging.Operations.DeleteVisit,
            ownerId,
            "Visit",
            visit.Id,
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

                    if (visitImages.Count > 0)
                    {
                        dbContext.VisitImages.RemoveRange(visitImages);
                    }

                    if (imageAssets.Count > 0)
                    {
                        dbContext.ImageAssets.RemoveRange(imageAssets);
                    }

                    owner.UsedImageBytes = remainingUsedImageBytes;
                    dbContext.Visits.Remove(visit);

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
                ApplicationOperationLogging.Operations.DeleteVisit,
                ownerId,
                "Visit",
                visit.Id,
                imageAssetCount: imageAssets.Count,
                storageTargetCount: storageTargets.Count,
                deletedReadyBytes: deletedReadyBytes);
        }
        catch (Exception ex)
        {
            ApplicationOperationLogging.LogDeletionFailed(
                logger,
                ex,
                ApplicationOperationLogging.Operations.DeleteVisit,
                ownerId,
                "Visit",
                visit.Id,
                imageAssetCount: imageAssets.Count,
                storageTargetCount: storageTargets.Count,
                deletedReadyBytes: deletedReadyBytes);

            throw;
        }

        await DeleteImageFilesBestEffortAsync(visit.Id, ownerId, storageTargets, cancellationToken);
    }

    private async Task DeleteImageFilesBestEffortAsync(
        int visitId,
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
                    "Visit",
                    visitId,
                    ImageOperationLogging.Phases.CascadeDelete,
                    target.ImageId,
                    target.StorageKey);
            }
        }
    }

    private sealed record StorageDeletionTarget(Guid ImageId, string StorageKey, string Category);
}

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

        var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            if (visitImages.Count > 0)
            {
                dbContext.VisitImages.RemoveRange(visitImages);
            }

            if (imageAssets.Count > 0)
            {
                dbContext.ImageAssets.RemoveRange(imageAssets);
            }

            owner.UsedImageBytes = Math.Max(0, owner.UsedImageBytes - deletedReadyBytes);
            dbContext.Visits.Remove(visit);

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
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

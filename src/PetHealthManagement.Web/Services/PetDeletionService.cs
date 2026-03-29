using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public class PetDeletionService(
    ApplicationDbContext dbContext,
    IImageStorageService imageStorageService,
    ILogger<PetDeletionService> logger) : IPetDeletionService
{
    public async Task DeleteAsync(
        Pet pet,
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var owner = dbContext.Users.Local.FirstOrDefault(x => x.Id == ownerId)
            ?? await dbContext.Users.FirstOrDefaultAsync(x => x.Id == ownerId, cancellationToken);

        if (owner is null)
        {
            throw new InvalidOperationException("Owner user was not found.");
        }

        var healthLogs = await dbContext.HealthLogs
            .Where(x => x.PetId == pet.Id)
            .ToListAsync(cancellationToken);

        var visits = await dbContext.Visits
            .Where(x => x.PetId == pet.Id)
            .ToListAsync(cancellationToken);

        var scheduleItems = await dbContext.ScheduleItems
            .Where(x => x.PetId == pet.Id)
            .ToListAsync(cancellationToken);

        var healthLogIds = healthLogs
            .Select(x => x.Id)
            .ToList();

        var visitIds = visits
            .Select(x => x.Id)
            .ToList();

        var healthLogImages = healthLogIds.Count == 0
            ? []
            : await dbContext.HealthLogImages
                .Include(x => x.Image)
                .Where(x => healthLogIds.Contains(x.HealthLogId))
                .ToListAsync(cancellationToken);

        var visitImages = visitIds.Count == 0
            ? []
            : await dbContext.VisitImages
                .Include(x => x.Image)
                .Where(x => visitIds.Contains(x.VisitId))
                .ToListAsync(cancellationToken);

        var petPhoto = pet.PhotoImageId is Guid photoImageId
            ? await dbContext.ImageAssets.FirstOrDefaultAsync(x => x.ImageId == photoImageId, cancellationToken)
            : null;

        var imageAssets = healthLogImages
            .Select(x => x.Image)
            .Concat(visitImages.Select(x => x.Image))
            .Concat(petPhoto is null ? [] : [petPhoto])
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
            if (healthLogImages.Count > 0)
            {
                dbContext.HealthLogImages.RemoveRange(healthLogImages);
            }

            if (visitImages.Count > 0)
            {
                dbContext.VisitImages.RemoveRange(visitImages);
            }

            if (imageAssets.Count > 0)
            {
                dbContext.ImageAssets.RemoveRange(imageAssets);
            }

            if (healthLogs.Count > 0)
            {
                dbContext.HealthLogs.RemoveRange(healthLogs);
            }

            if (visits.Count > 0)
            {
                dbContext.Visits.RemoveRange(visits);
            }

            if (scheduleItems.Count > 0)
            {
                dbContext.ScheduleItems.RemoveRange(scheduleItems);
            }

            owner.UsedImageBytes = Math.Max(0, owner.UsedImageBytes - deletedReadyBytes);
            dbContext.Pets.Remove(pet);

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

        await DeleteImageFilesBestEffortAsync(pet.Id, ownerId, storageTargets, cancellationToken);
    }

    private async Task DeleteImageFilesBestEffortAsync(
        int petId,
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
                    "Pet",
                    petId,
                    ImageOperationLogging.Phases.CascadeDelete,
                    target.ImageId,
                    target.StorageKey);
            }
        }
    }

    private sealed record StorageDeletionTarget(Guid ImageId, string StorageKey, string Category);
}

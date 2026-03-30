using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public class UserDataDeletionService(
    ApplicationDbContext dbContext,
    IImageStorageService imageStorageService,
    ILogger<UserDataDeletionService> logger) : IUserDataDeletionService
{
    public async Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var user = dbContext.Users.Local.FirstOrDefault(x => x.Id == userId)
            ?? await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            ApplicationOperationLogging.LogDeletionTargetNotFound(
                logger,
                ApplicationOperationLogging.Operations.DeleteUserData,
                userId,
                "User",
                userId);
            return false;
        }

        var pets = await dbContext.Pets
            .Where(x => x.OwnerId == userId)
            .ToListAsync(cancellationToken);

        var petIds = pets
            .Select(x => x.Id)
            .ToList();

        var healthLogs = petIds.Count == 0
            ? []
            : await dbContext.HealthLogs
                .Where(x => petIds.Contains(x.PetId))
                .ToListAsync(cancellationToken);

        var healthLogIds = healthLogs
            .Select(x => x.Id)
            .ToList();

        var visits = petIds.Count == 0
            ? []
            : await dbContext.Visits
                .Where(x => petIds.Contains(x.PetId))
                .ToListAsync(cancellationToken);

        var visitIds = visits
            .Select(x => x.Id)
            .ToList();

        var scheduleItems = petIds.Count == 0
            ? []
            : await dbContext.ScheduleItems
                .Where(x => petIds.Contains(x.PetId))
                .ToListAsync(cancellationToken);

        var healthLogImages = healthLogIds.Count == 0
            ? []
            : await dbContext.HealthLogImages
                .Where(x => healthLogIds.Contains(x.HealthLogId))
                .ToListAsync(cancellationToken);

        var visitImages = visitIds.Count == 0
            ? []
            : await dbContext.VisitImages
                .Where(x => visitIds.Contains(x.VisitId))
                .ToListAsync(cancellationToken);

        var imageAssets = await dbContext.ImageAssets
            .Where(x => x.OwnerId == userId)
            .ToListAsync(cancellationToken);

        var storageTargets = imageAssets
            .Where(x => !string.IsNullOrWhiteSpace(x.StorageKey))
            .Select(x => new StorageDeletionTarget(x.ImageId, x.StorageKey, x.Category))
            .ToList();

        ApplicationOperationLogging.LogDeletionStarted(
            logger,
            ApplicationOperationLogging.Operations.DeleteUserData,
            userId,
            "User",
            userId,
            petCount: pets.Count,
            healthLogCount: healthLogs.Count,
            visitCount: visits.Count,
            scheduleItemCount: scheduleItems.Count,
            imageAssetCount: imageAssets.Count,
            storageTargetCount: storageTargets.Count);

        user.AvatarImageId = null;

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

            if (pets.Count > 0)
            {
                dbContext.Pets.RemoveRange(pets);
            }

            dbContext.Users.Remove(user);

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            ApplicationOperationLogging.LogDeletionCompleted(
                logger,
                ApplicationOperationLogging.Operations.DeleteUserData,
                userId,
                "User",
                userId,
                petCount: pets.Count,
                healthLogCount: healthLogs.Count,
                visitCount: visits.Count,
                scheduleItemCount: scheduleItems.Count,
                imageAssetCount: imageAssets.Count,
                storageTargetCount: storageTargets.Count);
        }
        catch (Exception ex)
        {
            ApplicationOperationLogging.LogDeletionFailed(
                logger,
                ex,
                ApplicationOperationLogging.Operations.DeleteUserData,
                userId,
                "User",
                userId,
                petCount: pets.Count,
                healthLogCount: healthLogs.Count,
                visitCount: visits.Count,
                scheduleItemCount: scheduleItems.Count,
                imageAssetCount: imageAssets.Count,
                storageTargetCount: storageTargets.Count);

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

        await DeleteImageFilesBestEffortAsync(userId, storageTargets, cancellationToken);
        return true;
    }

    private async Task DeleteImageFilesBestEffortAsync(
        string userId,
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
                    userId,
                    "User",
                    userId,
                    ImageOperationLogging.Phases.CascadeDelete,
                    target.ImageId,
                    target.StorageKey);
            }
        }
    }

    private sealed record StorageDeletionTarget(Guid ImageId, string StorageKey, string Category);
}

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
            return false;
        }

        var pets = await dbContext.Pets
            .Where(x => x.OwnerId == userId)
            .ToListAsync(cancellationToken);

        var petIds = pets
            .Select(x => x.Id)
            .ToList();

        var healthLogs = await dbContext.HealthLogs
            .Where(x => petIds.Contains(x.PetId))
            .ToListAsync(cancellationToken);

        var healthLogIds = healthLogs
            .Select(x => x.Id)
            .ToList();

        var healthLogImages = await dbContext.HealthLogImages
            .Where(x => healthLogIds.Contains(x.HealthLogId))
            .ToListAsync(cancellationToken);

        var imageAssets = await dbContext.ImageAssets
            .Where(x => x.OwnerId == userId)
            .ToListAsync(cancellationToken);

        var storageTargets = imageAssets
            .Where(x => !string.IsNullOrWhiteSpace(x.StorageKey))
            .Select(x => new StorageDeletionTarget(x.ImageId, x.StorageKey))
            .ToList();

        user.AvatarImageId = null;

        var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            dbContext.HealthLogImages.RemoveRange(healthLogImages);
            dbContext.HealthLogs.RemoveRange(healthLogs);
            dbContext.Pets.RemoveRange(pets);
            dbContext.ImageAssets.RemoveRange(imageAssets);
            dbContext.Users.Remove(user);

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
                logger.LogWarning(
                    ex,
                    "Failed to delete image file while deleting user data. userId={UserId} imageId={ImageId} storageKey={StorageKey}",
                    userId,
                    target.ImageId,
                    target.StorageKey);
            }
        }
    }

    private sealed record StorageDeletionTarget(Guid ImageId, string StorageKey);
}

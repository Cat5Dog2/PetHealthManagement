using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public class OwnershipAuthorizer(ApplicationDbContext dbContext) : IOwnershipAuthorizer
{
    public async Task<Pet?> FindOwnedPetAsync(
        int petId,
        string userId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Pets.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(x => x.Id == petId && x.OwnerId == userId, cancellationToken);
    }

    public async Task<HealthLog?> FindOwnedHealthLogAsync(
        int healthLogId,
        string userId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.HealthLogs
            .Include(x => x.Pet)
            .AsQueryable();

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(
            x => x.Id == healthLogId && x.Pet.OwnerId == userId,
            cancellationToken);
    }

    public async Task<Visit?> FindOwnedVisitAsync(
        int visitId,
        string userId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Visits
            .Include(x => x.Pet)
            .AsQueryable();

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(
            x => x.Id == visitId && x.Pet.OwnerId == userId,
            cancellationToken);
    }

    public async Task<ScheduleItem?> FindOwnedScheduleItemAsync(
        int scheduleItemId,
        string userId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ScheduleItems
            .Include(x => x.Pet)
            .AsQueryable();

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(
            x => x.Id == scheduleItemId && x.Pet.OwnerId == userId,
            cancellationToken);
    }

    public async Task<ImageAsset?> FindReadableImageAssetAsync(
        Guid imageId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var imageAsset = await dbContext.ImageAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.ImageId == imageId && x.Status == ImageAssetStatus.Ready,
                cancellationToken);

        if (imageAsset is null)
        {
            return null;
        }

        return await IsAuthorizedToReadImageAsync(imageAsset, userId, cancellationToken)
            ? imageAsset
            : null;
    }

    private async Task<bool> IsAuthorizedToReadImageAsync(
        ImageAsset imageAsset,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(imageAsset.Category, "Avatar", StringComparison.Ordinal))
        {
            return await dbContext.Users
                .AsNoTracking()
                .AnyAsync(
                    x => x.AvatarImageId == imageAsset.ImageId && x.Id == userId,
                    cancellationToken);
        }

        if (string.Equals(imageAsset.Category, "HealthLog", StringComparison.Ordinal))
        {
            return await dbContext.HealthLogImages
                .AsNoTracking()
                .Where(x => x.ImageId == imageAsset.ImageId)
                .AnyAsync(x => x.HealthLog.Pet.OwnerId == userId, cancellationToken);
        }

        if (string.Equals(imageAsset.Category, "Visit", StringComparison.Ordinal))
        {
            return await dbContext.VisitImages
                .AsNoTracking()
                .Where(x => x.ImageId == imageAsset.ImageId)
                .AnyAsync(x => x.Visit.Pet.OwnerId == userId, cancellationToken);
        }

        if (!string.Equals(imageAsset.Category, "PetPhoto", StringComparison.Ordinal))
        {
            return false;
        }

        return await dbContext.Pets
            .AsNoTracking()
            .Where(x => x.PhotoImageId == imageAsset.ImageId)
            .AnyAsync(x => x.OwnerId == userId || x.IsPublic, cancellationToken);
    }
}

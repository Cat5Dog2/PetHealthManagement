using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public interface IOwnershipAuthorizer
{
    Task<Pet?> FindOwnedPetAsync(
        int petId,
        string userId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<HealthLog?> FindOwnedHealthLogAsync(
        int healthLogId,
        string userId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<Visit?> FindOwnedVisitAsync(
        int visitId,
        string userId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<ScheduleItem?> FindOwnedScheduleItemAsync(
        int scheduleItemId,
        string userId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<ImageAsset?> FindReadableImageAssetAsync(
        Guid imageId,
        string userId,
        CancellationToken cancellationToken = default);
}

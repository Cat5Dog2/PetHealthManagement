using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public interface IHealthLogDeletionService
{
    Task DeleteAsync(
        HealthLog healthLog,
        string ownerId,
        CancellationToken cancellationToken = default);
}

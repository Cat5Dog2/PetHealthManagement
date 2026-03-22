using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public interface IVisitDeletionService
{
    Task DeleteAsync(
        Visit visit,
        string ownerId,
        CancellationToken cancellationToken = default);
}

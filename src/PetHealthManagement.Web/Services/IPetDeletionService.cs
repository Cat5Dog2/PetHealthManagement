using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public interface IPetDeletionService
{
    Task DeleteAsync(Pet pet, string ownerId, CancellationToken cancellationToken = default);
}

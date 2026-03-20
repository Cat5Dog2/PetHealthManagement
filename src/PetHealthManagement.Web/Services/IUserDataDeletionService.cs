namespace PetHealthManagement.Web.Services;

public interface IUserDataDeletionService
{
    Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken = default);
}

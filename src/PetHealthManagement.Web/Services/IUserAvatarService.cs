using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public interface IUserAvatarService
{
    Task<UserAvatarUpdateResult> ApplyAvatarChangeAsync(
        ApplicationUser user,
        IFormFile? newAvatarFile,
        CancellationToken cancellationToken = default);
}

public sealed record UserAvatarUpdateResult(bool Succeeded, string? ErrorMessage)
{
    public static UserAvatarUpdateResult Success()
        => new(true, null);

    public static UserAvatarUpdateResult Fail(string errorMessage)
        => new(false, errorMessage);
}

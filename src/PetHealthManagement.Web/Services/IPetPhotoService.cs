using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public interface IPetPhotoService
{
    Task<PetPhotoUpdateResult> ApplyPetPhotoChangeAsync(
        Pet pet,
        string ownerId,
        IFormFile? newPhotoFile,
        bool removePhoto,
        CancellationToken cancellationToken = default);
}

public sealed record PetPhotoUpdateResult(bool Succeeded, string? ErrorMessage)
{
    public static PetPhotoUpdateResult Success() => new(true, null);

    public static PetPhotoUpdateResult Fail(string errorMessage) => new(false, errorMessage);
}

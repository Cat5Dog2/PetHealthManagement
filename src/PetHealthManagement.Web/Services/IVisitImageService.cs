using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public interface IVisitImageService
{
    Task<VisitImageUpdateResult> ApplyImageChangesAsync(
        Visit visit,
        string ownerId,
        IReadOnlyCollection<IFormFile>? newFiles,
        IReadOnlyCollection<Guid>? deleteImageIds,
        CancellationToken cancellationToken = default);
}

public sealed record VisitImageUpdateResult(bool Succeeded, string? ErrorMessage)
{
    public static VisitImageUpdateResult Success() => new(true, null);

    public static VisitImageUpdateResult Fail(string errorMessage) => new(false, errorMessage);
}

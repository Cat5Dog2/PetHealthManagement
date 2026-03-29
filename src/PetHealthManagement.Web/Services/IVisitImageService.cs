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

public sealed record VisitImageUpdateResult(bool Succeeded, string? ErrorMessage, bool IsConcurrencyConflict)
{
    public static VisitImageUpdateResult Success() => new(true, null, false);

    public static VisitImageUpdateResult Fail(string errorMessage) => new(false, errorMessage, false);

    public static VisitImageUpdateResult ConcurrencyConflict() => new(false, null, true);
}

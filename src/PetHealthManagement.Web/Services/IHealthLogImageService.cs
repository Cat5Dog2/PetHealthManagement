using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public interface IHealthLogImageService
{
    Task<HealthLogImageUpdateResult> ApplyImageChangesAsync(
        HealthLog healthLog,
        string ownerId,
        IReadOnlyCollection<IFormFile>? newFiles,
        IReadOnlyCollection<Guid>? deleteImageIds,
        CancellationToken cancellationToken = default);
}

public sealed record HealthLogImageUpdateResult(bool Succeeded, string? ErrorMessage, bool IsConcurrencyConflict)
{
    public static HealthLogImageUpdateResult Success() => new(true, null, false);

    public static HealthLogImageUpdateResult Fail(string errorMessage) => new(false, errorMessage, false);

    public static HealthLogImageUpdateResult ConcurrencyConflict() => new(false, null, true);
}

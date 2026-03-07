namespace PetHealthManagement.Web.Services;

public interface IImageStorageService
{
    string CreateTemporaryPath(string extension);

    Task SaveFormFileToPathAsync(IFormFile file, string destinationPath, CancellationToken cancellationToken = default);

    Task MoveToStorageAsync(string sourcePath, string storageKey, CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default);
}

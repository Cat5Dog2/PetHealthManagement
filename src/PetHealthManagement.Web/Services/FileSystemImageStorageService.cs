using Microsoft.Extensions.Options;

namespace PetHealthManagement.Web.Services;

public class FileSystemImageStorageService(
    IWebHostEnvironment hostEnvironment,
    IOptions<StorageOptions> options) : IImageStorageService
{
    private readonly string _storageRoot = ResolveStorageRoot(hostEnvironment.ContentRootPath, options.Value.RootPath);

    public string CreateTemporaryPath(string extension)
    {
        var sanitizedExtension = extension.StartsWith('.') ? extension : $".{extension}";
        var tmpDirectory = Path.Combine(_storageRoot, "tmp");
        Directory.CreateDirectory(tmpDirectory);
        return Path.Combine(tmpDirectory, $"{Guid.NewGuid():N}{sanitizedExtension}");
    }

    public async Task SaveFormFileToPathAsync(IFormFile file, string destinationPath, CancellationToken cancellationToken = default)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        await using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(destinationStream, cancellationToken);
    }

    public Task MoveToStorageAsync(string sourcePath, string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var absoluteStoragePath = ResolveStoragePath(storageKey);
        var destinationDirectory = Path.GetDirectoryName(absoluteStoragePath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        if (File.Exists(absoluteStoragePath))
        {
            File.Delete(absoluteStoragePath);
        }

        File.Move(sourcePath, absoluteStoragePath);
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var absoluteStoragePath = ResolveStoragePath(storageKey);
        if (!File.Exists(absoluteStoragePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(absoluteStoragePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var absoluteStoragePath = ResolveStoragePath(storageKey);
        if (File.Exists(absoluteStoragePath))
        {
            File.Delete(absoluteStoragePath);
        }

        return Task.CompletedTask;
    }

    private static string ResolveStorageRoot(string contentRootPath, string configuredRootPath)
    {
        if (Path.IsPathRooted(configuredRootPath))
        {
            return configuredRootPath;
        }

        return Path.Combine(contentRootPath, configuredRootPath);
    }

    private string ResolveStoragePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("Storage key is required.", nameof(storageKey));
        }

        if (Path.IsPathRooted(storageKey) || storageKey.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Storage key must be a relative path.", nameof(storageKey));
        }

        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_storageRoot, normalized);
    }
}

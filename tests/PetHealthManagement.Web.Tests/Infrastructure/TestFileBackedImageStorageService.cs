using Microsoft.AspNetCore.Http;
using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Tests.Infrastructure;

internal sealed class TestFileBackedImageStorageService : IImageStorageService, IDisposable
{
    private readonly TemporaryStorageRoot storageRoot;

    public TestFileBackedImageStorageService(string prefix)
    {
        storageRoot = new TemporaryStorageRoot(prefix);
    }

    public HashSet<string> FailingDeleteStorageKeys { get; init; } = [];

    public List<string> DeletedStorageKeys { get; } = [];

    public List<string> MovedStorageKeys { get; } = [];

    public string RootPath => storageRoot.RootPath;

    public string CreateTemporaryPath(string extension)
    {
        var tempDirectory = Path.Combine(RootPath, "tmp");
        Directory.CreateDirectory(tempDirectory);
        return Path.Combine(tempDirectory, $"{Guid.NewGuid():N}{extension}");
    }

    public async Task SaveFormFileToPathAsync(IFormFile file, string destinationPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        await using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(destination, cancellationToken);
    }

    public Task MoveToStorageAsync(string sourcePath, string storageKey, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        MovedStorageKeys.Add(storageKey);
        var destinationPath = Path.Combine(RootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
        File.Delete(sourcePath);
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        _ = storageKey;
        _ = cancellationToken;
        throw new NotSupportedException();
    }

    public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        DeletedStorageKeys.Add(storageKey);

        if (FailingDeleteStorageKeys.Contains(storageKey))
        {
            throw new IOException("Simulated delete failure.");
        }

        var path = Path.Combine(RootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        storageRoot.Dispose();
    }
}

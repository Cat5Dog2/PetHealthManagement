namespace PetHealthManagement.Web.Tests.Infrastructure;

internal sealed class TemporaryStorageRoot : IDisposable
{
    public TemporaryStorageRoot(string prefix)
    {
        RootPath = Path.Combine(Path.GetTempPath(), prefix, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup for test storage.
        }
    }
}

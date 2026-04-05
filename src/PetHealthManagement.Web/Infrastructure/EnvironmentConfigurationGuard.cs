using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Infrastructure;

public static class EnvironmentConfigurationGuard
{
    public static void Validate(IConfiguration configuration, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'DefaultConnection' is required for {environmentName}. Configure it in appsettings.{environmentName}.json, user-secrets, or the ConnectionStrings__DefaultConnection environment variable.");
        }

        var storageRoot = configuration.GetSection("Storage").GetValue<string>(nameof(StorageOptions.RootPath));
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new InvalidOperationException(
                $"Storage:RootPath is required for {environmentName}. Configure it in appsettings.{environmentName}.json or the Storage__RootPath environment variable.");
        }

        if (!string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            && connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{environmentName} cannot use the Development LocalDB connection string. Configure ConnectionStrings__DefaultConnection for this environment.");
        }

        ValidateDataProtection(configuration, environmentName);
    }

    private static void ValidateDataProtection(IConfiguration configuration, string environmentName)
    {
        var section = configuration.GetSection(DataProtectionKeyManagementOptions.SectionName);
        var blobUri = section.GetValue<string>(nameof(DataProtectionKeyManagementOptions.BlobUri));
        var keyVaultKeyIdentifier = section.GetValue<string>(nameof(DataProtectionKeyManagementOptions.KeyVaultKeyIdentifier));

        var hasBlobUri = !string.IsNullOrWhiteSpace(blobUri);
        var hasKeyVaultKeyIdentifier = !string.IsNullOrWhiteSpace(keyVaultKeyIdentifier);

        if (hasBlobUri != hasKeyVaultKeyIdentifier)
        {
            throw new InvalidOperationException(
                "DataProtection requires both DataProtection__BlobUri and DataProtection__KeyVaultKeyIdentifier to be configured together.");
        }

        if (!hasBlobUri)
        {
            if (!string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{environmentName} requires DataProtection key persistence. Configure DataProtection__BlobUri and DataProtection__KeyVaultKeyIdentifier.");
            }

            return;
        }

        if (!Uri.TryCreate(blobUri, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                "DataProtection__BlobUri must be an absolute URI.");
        }

        if (!Uri.TryCreate(keyVaultKeyIdentifier, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                "DataProtection__KeyVaultKeyIdentifier must be an absolute URI.");
        }
    }
}

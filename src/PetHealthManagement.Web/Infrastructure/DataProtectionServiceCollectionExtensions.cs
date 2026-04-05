using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;

namespace PetHealthManagement.Web.Infrastructure;

public static class DataProtectionServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguredDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<DataProtectionKeyManagementOptions>(
            configuration.GetSection(DataProtectionKeyManagementOptions.SectionName));

        var options = configuration
            .GetSection(DataProtectionKeyManagementOptions.SectionName)
            .Get<DataProtectionKeyManagementOptions>()
            ?? new DataProtectionKeyManagementOptions();

        var builder = services
            .AddDataProtection()
            .SetApplicationName(ResolveApplicationName(options));

        if (!TryGetAzurePersistenceUris(options, out var blobUri, out var keyVaultKeyIdentifier))
        {
            return services;
        }

        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(options.ManagedIdentityClientId))
        {
            credentialOptions.ManagedIdentityClientId = options.ManagedIdentityClientId;
        }

        var credential = new DefaultAzureCredential(credentialOptions);

        builder
            .PersistKeysToAzureBlobStorage(blobUri, credential)
            .ProtectKeysWithAzureKeyVault(keyVaultKeyIdentifier, credential);

        return services;
    }

    private static string ResolveApplicationName(DataProtectionKeyManagementOptions options)
    {
        return string.IsNullOrWhiteSpace(options.ApplicationName)
            ? DataProtectionKeyManagementOptions.DefaultApplicationName
            : options.ApplicationName.Trim();
    }

    private static bool TryGetAzurePersistenceUris(
        DataProtectionKeyManagementOptions options,
        out Uri blobUri,
        out Uri keyVaultKeyIdentifier)
    {
        var hasBlobUri = !string.IsNullOrWhiteSpace(options.BlobUri);
        var hasKeyVaultKeyIdentifier = !string.IsNullOrWhiteSpace(options.KeyVaultKeyIdentifier);

        if (hasBlobUri != hasKeyVaultKeyIdentifier)
        {
            throw new InvalidOperationException(
                "DataProtection requires both BlobUri and KeyVaultKeyIdentifier to be configured together.");
        }

        if (!hasBlobUri)
        {
            blobUri = null!;
            keyVaultKeyIdentifier = null!;
            return false;
        }

        blobUri = new Uri(options.BlobUri!, UriKind.Absolute);
        keyVaultKeyIdentifier = new Uri(options.KeyVaultKeyIdentifier!, UriKind.Absolute);
        return true;
    }
}

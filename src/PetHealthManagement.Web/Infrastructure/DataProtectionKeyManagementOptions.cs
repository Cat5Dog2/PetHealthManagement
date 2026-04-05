namespace PetHealthManagement.Web.Infrastructure;

public sealed class DataProtectionKeyManagementOptions
{
    public const string SectionName = "DataProtection";
    public const string DefaultApplicationName = "PetHealthManagement.Web";

    public string ApplicationName { get; set; } = DefaultApplicationName;

    public string? BlobUri { get; set; }

    public string? KeyVaultKeyIdentifier { get; set; }

    public string? ManagedIdentityClientId { get; set; }
}

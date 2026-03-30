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
    }
}

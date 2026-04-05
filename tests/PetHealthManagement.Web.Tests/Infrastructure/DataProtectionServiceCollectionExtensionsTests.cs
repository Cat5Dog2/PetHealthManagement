using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PetHealthManagement.Web.Infrastructure;

namespace PetHealthManagement.Web.Tests.Infrastructure;

public class DataProtectionServiceCollectionExtensionsTests
{
    [Fact]
    public void AddConfiguredDataProtection_SetsDefaultApplicationName()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();
        var services = new ServiceCollection();

        services.AddConfiguredDataProtection(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value;

        Assert.Equal(DataProtectionKeyManagementOptions.DefaultApplicationName, options.ApplicationDiscriminator);
    }

    [Fact]
    public void AddConfiguredDataProtection_AllowsAzureBlobAndKeyVaultConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:ApplicationName"] = "PetHealthManagement.Web",
                ["DataProtection:BlobUri"] = "https://pethealthstorage.blob.core.windows.net/dataprotection/keys.xml",
                ["DataProtection:KeyVaultKeyIdentifier"] = "https://pethealth-kv.vault.azure.net/keys/data-protection"
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Record.Exception(() => services.AddConfiguredDataProtection(configuration));

        Assert.Null(exception);
    }
}

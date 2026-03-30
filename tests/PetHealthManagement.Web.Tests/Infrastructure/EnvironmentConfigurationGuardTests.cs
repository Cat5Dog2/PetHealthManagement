using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PetHealthManagement.Web.Infrastructure;

namespace PetHealthManagement.Web.Tests.Infrastructure;

public class EnvironmentConfigurationGuardTests
{
    [Theory]
    [InlineData("Development", "StorageRoot/Development", "Server=(localdb)\\mssqllocaldb;Database=PetHealthManagement;Trusted_Connection=True;MultipleActiveResultSets=true")]
    [InlineData("Staging", "StorageRoot/Staging", null)]
    [InlineData("Production", "StorageRoot/Production", null)]
    public void EnvironmentSpecificAppSettings_AreLoaded(string environmentName, string expectedStorageRoot, string? expectedConnectionString)
    {
        var configuration = BuildConfiguration(environmentName);

        Assert.Equal(expectedStorageRoot, configuration["Storage:RootPath"]);
        Assert.Equal(expectedConnectionString, configuration.GetConnectionString("DefaultConnection"));
    }

    [Fact]
    public void Validate_AllowsDevelopmentConfiguration()
    {
        var configuration = BuildConfiguration(Environments.Development);

        var exception = Record.Exception(() => EnvironmentConfigurationGuard.Validate(configuration, Environments.Development));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_RejectsMissingConnectionStringOutsideDevelopment()
    {
        var configuration = BuildConfiguration("Staging");

        var exception = Assert.Throws<InvalidOperationException>(() => EnvironmentConfigurationGuard.Validate(configuration, "Staging"));

        Assert.Contains("ConnectionStrings__DefaultConnection", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsLocalDbOutsideDevelopment()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=PetHealthManagement;Trusted_Connection=True;MultipleActiveResultSets=true",
                ["Storage:RootPath"] = "StorageRoot/Staging"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => EnvironmentConfigurationGuard.Validate(configuration, "Production"));

        Assert.Contains("cannot use the Development LocalDB connection string", exception.Message, StringComparison.Ordinal);
    }

    private static IConfigurationRoot BuildConfiguration(string environmentName)
    {
        return new ConfigurationBuilder()
            .SetBasePath(GetWebProjectPath())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .Build();
    }

    private static string GetWebProjectPath()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "PetHealthManagement.Web"));
    }
}

using Microsoft.Extensions.Configuration;
using PetHealthManagement.Web.Infrastructure;

namespace PetHealthManagement.Web.Tests.Infrastructure;

public class DotEnvConfigurationTests
{
    [Fact]
    public void Add_LoadsDevelopmentSetupKeys_FromConfiguredEnvFile()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var envFilePath = tempDirectory.WriteEnvFile(
        [
            "DevelopmentSetup__AdminEmail=admin@example.com",
            "DevelopmentSetup__AdminPassword='Admin123!'",
            "DevelopmentSetup__AdminDisplayName=\"Production Admin\""
        ]);
        using var envFileVariable = TemporaryEnvironmentVariable.Set(DotEnvConfiguration.EnvFilePathVariableName, envFilePath);
        var configuration = new ConfigurationManager();

        DotEnvConfiguration.Add(configuration, tempDirectory.Path);

        Assert.Equal("admin@example.com", configuration["DevelopmentSetup:AdminEmail"]);
        Assert.Equal("Admin123!", configuration["DevelopmentSetup:AdminPassword"]);
        Assert.Equal("Production Admin", configuration["DevelopmentSetup:AdminDisplayName"]);
    }

    [Fact]
    public void Add_MapsAdminAliases_ToDevelopmentSetupOptions()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var envFilePath = tempDirectory.WriteEnvFile(
        [
            "ADMIN_EMAIL=admin@example.com",
            "PRODUCTION_ADMIN_PASSWORD=Admin123!",
            "ADMIN_DISPLAY_NAME=Production Admin"
        ]);
        using var envFileVariable = TemporaryEnvironmentVariable.Set(DotEnvConfiguration.EnvFilePathVariableName, envFilePath);
        var configuration = new ConfigurationManager();

        DotEnvConfiguration.Add(configuration, tempDirectory.Path);

        Assert.Equal("admin@example.com", configuration["DevelopmentSetup:AdminEmail"]);
        Assert.Equal("Admin123!", configuration["DevelopmentSetup:AdminPassword"]);
        Assert.Equal("Production Admin", configuration["DevelopmentSetup:AdminDisplayName"]);
    }

    [Fact]
    public void Add_DoesNotOverrideEnvironmentVariables()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var envFilePath = tempDirectory.WriteEnvFile(["DotEnvConfigurationTests__Value=from-file"]);
        using var envFileVariable = TemporaryEnvironmentVariable.Set(DotEnvConfiguration.EnvFilePathVariableName, envFilePath);
        using var valueVariable = TemporaryEnvironmentVariable.Set("DotEnvConfigurationTests__Value", "from-env");
        var configuration = new ConfigurationManager();
        configuration.AddEnvironmentVariables();

        DotEnvConfiguration.Add(configuration, tempDirectory.Path);

        Assert.Equal("from-env", configuration["DotEnvConfigurationTests:Value"]);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pethealth-dotenv-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public string WriteEnvFile(IEnumerable<string> lines)
        {
            var envFilePath = System.IO.Path.Combine(Path, ".env");
            File.WriteAllLines(envFilePath, lines);
            return envFilePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class TemporaryEnvironmentVariable : IDisposable
    {
        private readonly string name;
        private readonly string? originalValue;

        private TemporaryEnvironmentVariable(string name, string? value)
        {
            this.name = name;
            originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public static TemporaryEnvironmentVariable Set(string name, string value)
        {
            return new TemporaryEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(name, originalValue);
        }
    }
}

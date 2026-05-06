using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PetHealthManagement.Web.Infrastructure;

namespace PetHealthManagement.Web.Tests.Infrastructure;

public class AzureMonitorTelemetryServiceCollectionExtensionsTests
{
    [Fact]
    public void AddConfiguredAzureMonitorTelemetry_AllowsMissingConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureMonitor:ServiceName"] = "PetHealthManagement.Web"
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Record.Exception(() => services.AddConfiguredAzureMonitorTelemetry(configuration));

        Assert.Null(exception);
    }

    [Fact]
    public void AddConfiguredAzureMonitorTelemetry_PrefersApplicationInsightsEnvironmentVariable()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=env-key",
                ["AzureMonitor:ConnectionString"] = "InstrumentationKey=config-key",
                ["AzureMonitor:ServiceName"] = "Configured.Service"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddConfiguredAzureMonitorTelemetry(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AzureMonitorTelemetryOptions>>().Value;

        Assert.Equal("Configured.Service", options.ServiceName);
        Assert.False(options.EnableLiveMetrics);
    }

    [Fact]
    public void AddConfiguredAzureMonitorTelemetry_RejectsOutOfRangeSamplingRatio()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=env-key",
                ["AzureMonitor:SamplingRatio"] = "1.5"
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddConfiguredAzureMonitorTelemetry(configuration));

        Assert.Contains("AzureMonitor:SamplingRatio", exception.Message, StringComparison.Ordinal);
    }
}

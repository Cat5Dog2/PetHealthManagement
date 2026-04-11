using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Resources;

namespace PetHealthManagement.Web.Infrastructure;

public static class AzureMonitorTelemetryServiceCollectionExtensions
{
    private const string ApplicationInsightsConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    public static IServiceCollection AddConfiguredAzureMonitorTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AzureMonitorTelemetryOptions>(
            configuration.GetSection(AzureMonitorTelemetryOptions.SectionName));

        var options = configuration
            .GetSection(AzureMonitorTelemetryOptions.SectionName)
            .Get<AzureMonitorTelemetryOptions>()
            ?? new AzureMonitorTelemetryOptions();

        var connectionString = ResolveConnectionString(configuration, options);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return services;
        }

        var samplingRatio = NormalizeSamplingRatio(options.SamplingRatio);
        var serviceName = ResolveServiceName(options.ServiceName);

        var openTelemetryBuilder = services
            .AddOpenTelemetry()
            .UseAzureMonitor(azureMonitorOptions =>
            {
                azureMonitorOptions.ConnectionString = connectionString;
                azureMonitorOptions.EnableLiveMetrics = options.EnableLiveMetrics;

                if (samplingRatio.HasValue)
                {
                    azureMonitorOptions.SamplingRatio = samplingRatio.Value;
                }

                if (!string.IsNullOrWhiteSpace(options.StorageDirectory))
                {
                    azureMonitorOptions.StorageDirectory = options.StorageDirectory.Trim();
                }
            });

        openTelemetryBuilder.ConfigureResource(resourceBuilder =>
        {
            resourceBuilder.AddAttributes(new Dictionary<string, object>
            {
                ["service.name"] = serviceName
            });
        });

        return services;
    }

    private static string? ResolveConnectionString(
        IConfiguration configuration,
        AzureMonitorTelemetryOptions options)
    {
        var environmentConnectionString = configuration[ApplicationInsightsConnectionStringKey];
        if (!string.IsNullOrWhiteSpace(environmentConnectionString))
        {
            return environmentConnectionString.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return options.ConnectionString.Trim();
        }

        return null;
    }

    private static string ResolveServiceName(string? configuredServiceName)
    {
        return string.IsNullOrWhiteSpace(configuredServiceName)
            ? AzureMonitorTelemetryOptions.DefaultServiceName
            : configuredServiceName.Trim();
    }

    private static float? NormalizeSamplingRatio(float? samplingRatio)
    {
        if (!samplingRatio.HasValue)
        {
            return null;
        }

        if (samplingRatio.Value is < 0.0f or > 1.0f)
        {
            throw new InvalidOperationException(
                "AzureMonitor:SamplingRatio must be between 0.0 and 1.0.");
        }

        return samplingRatio.Value;
    }
}

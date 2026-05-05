namespace PetHealthManagement.Web.Infrastructure;

public sealed class AzureMonitorTelemetryOptions
{
    public const string SectionName = "AzureMonitor";
    public const string DefaultServiceName = "PetHealthManagement.Web";

    public string? ConnectionString { get; set; }

    public string ServiceName { get; set; } = DefaultServiceName;

    public bool EnableLiveMetrics { get; set; }

    public float? SamplingRatio { get; set; }

    public string? StorageDirectory { get; set; }
}

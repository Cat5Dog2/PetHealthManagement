using Microsoft.Extensions.Configuration;

namespace PetHealthManagement.Web.Infrastructure;

public static class DotEnvConfiguration
{
    public const string EnvFilePathVariableName = "DOTNET_ENV_FILE";
    public const string DefaultFileName = ".env";

    public static void Add(ConfigurationManager configuration, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var envFilePath = ResolveEnvFilePath(configuration, contentRootPath);
        if (!File.Exists(envFilePath))
        {
            return;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in Parse(File.ReadLines(envFilePath)))
        {
            var normalizedKey = NormalizeKey(key);
            if (HasEnvironmentVariable(normalizedKey))
            {
                continue;
            }

            values[normalizedKey] = value;
        }

        if (values.Count > 0)
        {
            configuration.AddInMemoryCollection(values);
        }
    }

    internal static IReadOnlyDictionary<string, string?> Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var lineNumber = 0;
        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            const string exportPrefix = "export ";
            if (line.StartsWith(exportPrefix, StringComparison.Ordinal))
            {
                line = line[exportPrefix.Length..].TrimStart();
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException($".env line {lineNumber} must use KEY=value format.");
            }

            var key = line[..separatorIndex].Trim();
            if (key.Length == 0)
            {
                throw new InvalidOperationException($".env line {lineNumber} has an empty key.");
            }

            var value = UnquoteValue(line[(separatorIndex + 1)..].Trim());
            values[key] = value;
        }

        return values;
    }

    private static string ResolveEnvFilePath(IConfiguration configuration, string contentRootPath)
    {
        var configuredPath = Environment.GetEnvironmentVariable(EnvFilePathVariableName);
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = configuration[EnvFilePathVariableName];
        }

        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? DefaultFileName
            : configuredPath.Trim();

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(contentRootPath, path));
    }

    private static string NormalizeKey(string key)
    {
        var normalized = key.Trim().Replace("__", ":", StringComparison.Ordinal);
        return normalized switch
        {
            "ADMIN_EMAIL" or "PRODUCTION_ADMIN_EMAIL" => "DevelopmentSetup:AdminEmail",
            "ADMIN_PASSWORD" or "PRODUCTION_ADMIN_PASSWORD" => "DevelopmentSetup:AdminPassword",
            "ADMIN_DISPLAY_NAME" or "PRODUCTION_ADMIN_DISPLAY_NAME" => "DevelopmentSetup:AdminDisplayName",
            _ => normalized
        };
    }

    private static bool HasEnvironmentVariable(string configurationKey)
    {
        var environmentKey = configurationKey.Replace(':', '_');
        return Environment.GetEnvironmentVariable(environmentKey) is not null
               || Environment.GetEnvironmentVariable(configurationKey.Replace(":", "__", StringComparison.Ordinal)) is not null;
    }

    private static string UnquoteValue(string value)
    {
        if (value.Length < 2)
        {
            return value;
        }

        if (value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1]
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\r", "\r", StringComparison.Ordinal)
                .Replace("\\t", "\t", StringComparison.Ordinal)
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        return value[0] == '\'' && value[^1] == '\''
            ? value[1..^1]
            : value;
    }
}

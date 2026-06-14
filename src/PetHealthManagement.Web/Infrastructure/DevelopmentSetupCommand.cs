using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Infrastructure;

public static class DevelopmentSetupCommand
{
    private const string ApplyMigrationsArgument = "--apply-migrations";
    private const string SeedAdminArgument = "--seed-admin";
    private const string SeedDemoDataArgument = "--seed-demo-data";
    private const string SeedDevelopmentArgument = "--seed-development";
    private const string SetupDevelopmentArgument = "--setup-development";

    public static async Task<bool> TryExecuteAsync(
        WebApplication app,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(args);

        var applyMigrations = ContainsArgument(args, ApplyMigrationsArgument);
        var seedAdmin = ContainsArgument(args, SeedAdminArgument);
        var seedDemoData = ContainsArgument(args, SeedDemoDataArgument);
        var seedDevelopment = ContainsArgument(args, SeedDevelopmentArgument);
        var setupDevelopment = ContainsArgument(args, SetupDevelopmentArgument);

        if (!applyMigrations && !seedAdmin && !seedDemoData && !seedDevelopment && !setupDevelopment)
        {
            return false;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DevelopmentSetupCommand");
        var developmentSetupService = scope.ServiceProvider.GetRequiredService<IDevelopmentSetupService>();

        if (setupDevelopment || applyMigrations)
        {
            await developmentSetupService.ApplyMigrationsAsync(cancellationToken);
        }

        var seedDevelopmentData = setupDevelopment || seedDevelopment;
        if (seedDevelopmentData)
        {
            await developmentSetupService.SeedDevelopmentIdentityAsync(cancellationToken);
            await developmentSetupService.SeedDevelopmentDemoDataAsync(cancellationToken);
        }
        else if (seedAdmin)
        {
            await developmentSetupService.SeedAdminIdentityAsync(cancellationToken);
        }

        if (seedDemoData && !seedDevelopmentData)
        {
            await developmentSetupService.SeedDemoDataAsync(cancellationToken);
        }

        logger.LogInformation(
            "Completed setup command execution. applyMigrations={ApplyMigrations} seedAdmin={SeedAdmin} seedDemoData={SeedDemoData} seedDevelopment={SeedDevelopment}",
            setupDevelopment || applyMigrations,
            seedAdmin && !seedDevelopmentData,
            seedDemoData && !seedDevelopmentData,
            seedDevelopmentData);

        return true;
    }

    private static bool ContainsArgument(IReadOnlyList<string> args, string expected)
    {
        return args.Any(arg => string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase));
    }
}

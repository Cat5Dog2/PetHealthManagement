using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Infrastructure;

public static class DevelopmentSetupCommand
{
    private const string ApplyMigrationsArgument = "--apply-migrations";
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
        var seedDevelopment = ContainsArgument(args, SeedDevelopmentArgument);
        var setupDevelopment = ContainsArgument(args, SetupDevelopmentArgument);

        if (!applyMigrations && !seedDevelopment && !setupDevelopment)
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

        if (setupDevelopment || seedDevelopment)
        {
            await developmentSetupService.SeedDevelopmentIdentityAsync(cancellationToken);
        }

        logger.LogInformation(
            "Completed setup command execution. applyMigrations={ApplyMigrations} seedDevelopment={SeedDevelopment}",
            setupDevelopment || applyMigrations,
            setupDevelopment || seedDevelopment);

        return true;
    }

    private static bool ContainsArgument(IReadOnlyList<string> args, string expected)
    {
        return args.Any(arg => string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase));
    }
}

namespace PetHealthManagement.Web.Services;

public interface IDevelopmentSetupService
{
    Task ApplyMigrationsAsync(CancellationToken cancellationToken = default);

    Task SeedAdminIdentityAsync(CancellationToken cancellationToken = default);

    Task SeedDevelopmentIdentityAsync(CancellationToken cancellationToken = default);

    Task SeedDemoDataAsync(CancellationToken cancellationToken = default);

    Task SeedDevelopmentDemoDataAsync(CancellationToken cancellationToken = default);
}

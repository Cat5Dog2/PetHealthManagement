namespace PetHealthManagement.Web.Services;

public interface IDevelopmentSetupService
{
    Task ApplyMigrationsAsync(CancellationToken cancellationToken = default);

    Task SeedDevelopmentIdentityAsync(CancellationToken cancellationToken = default);
}

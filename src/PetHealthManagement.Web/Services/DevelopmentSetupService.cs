using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public class DevelopmentSetupService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IHostEnvironment hostEnvironment,
    IOptions<DevelopmentSetupOptions> options,
    ILogger<DevelopmentSetupService> logger) : IDevelopmentSetupService
{
    public const string AdminRoleName = "Admin";

    public Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying EF Core migrations for environment {EnvironmentName}.", hostEnvironment.EnvironmentName);
        return dbContext.Database.MigrateAsync(cancellationToken);
    }

    public async Task SeedDevelopmentIdentityAsync(CancellationToken cancellationToken = default)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            throw new InvalidOperationException("Development identity seeding can only run in the Development environment.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!await roleManager.RoleExistsAsync(AdminRoleName))
        {
            var createRoleResult = await roleManager.CreateAsync(new IdentityRole(AdminRoleName));
            EnsureIdentitySucceeded(createRoleResult, "Failed to create the Admin role.");
            logger.LogInformation("Created development role {RoleName}.", AdminRoleName);
        }

        var adminEmail = options.Value.AdminEmail.Trim();
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            throw new InvalidOperationException(
                "DevelopmentSetup:AdminEmail is required. Configure it in user-secrets or via the DevelopmentSetup__AdminEmail environment variable.");
        }

        var adminDisplayName = string.IsNullOrWhiteSpace(options.Value.AdminDisplayName)
            ? "Development Admin"
            : options.Value.AdminDisplayName.Trim();

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            var adminPassword = options.Value.AdminPassword;
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                throw new InvalidOperationException(
                    "DevelopmentSetup:AdminPassword is required to create the development admin user. Configure it in user-secrets or via the DevelopmentSetup__AdminPassword environment variable.");
            }

            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = adminDisplayName
            };

            var createUserResult = await userManager.CreateAsync(adminUser, adminPassword);
            EnsureIdentitySucceeded(createUserResult, "Failed to create the development admin user.");
            logger.LogInformation("Created development admin user {AdminEmail}.", adminEmail);
        }
        else
        {
            var requiresUpdate = false;

            if (!string.Equals(adminUser.UserName, adminEmail, StringComparison.OrdinalIgnoreCase))
            {
                adminUser.UserName = adminEmail;
                requiresUpdate = true;
            }

            if (!string.Equals(adminUser.Email, adminEmail, StringComparison.OrdinalIgnoreCase))
            {
                adminUser.Email = adminEmail;
                requiresUpdate = true;
            }

            if (!adminUser.EmailConfirmed)
            {
                adminUser.EmailConfirmed = true;
                requiresUpdate = true;
            }

            if (string.IsNullOrWhiteSpace(adminUser.DisplayName))
            {
                adminUser.DisplayName = adminDisplayName;
                requiresUpdate = true;
            }

            if (requiresUpdate)
            {
                var updateUserResult = await userManager.UpdateAsync(adminUser);
                EnsureIdentitySucceeded(updateUserResult, "Failed to update the development admin user.");
            }

            if (!await userManager.HasPasswordAsync(adminUser))
            {
                var adminPassword = options.Value.AdminPassword;
                if (string.IsNullOrWhiteSpace(adminPassword))
                {
                    throw new InvalidOperationException(
                        "DevelopmentSetup:AdminPassword is required because the existing development admin user does not have a password.");
                }

                var addPasswordResult = await userManager.AddPasswordAsync(adminUser, adminPassword);
                EnsureIdentitySucceeded(addPasswordResult, "Failed to set the development admin password.");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, AdminRoleName))
        {
            var addToRoleResult = await userManager.AddToRoleAsync(adminUser, AdminRoleName);
            EnsureIdentitySucceeded(addToRoleResult, "Failed to add the development admin user to the Admin role.");
        }

        logger.LogInformation(
            "Development identity seed completed. adminEmail={AdminEmail} role={RoleName}",
            adminEmail,
            AdminRoleName);
    }

    private static void EnsureIdentitySucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var details = string.Join("; ", result.Errors.Select(x => $"{x.Code}: {x.Description}"));
        throw new InvalidOperationException($"{message} {details}");
    }
}

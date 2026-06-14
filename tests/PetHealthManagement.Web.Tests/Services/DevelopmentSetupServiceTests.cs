using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Services;

public class DevelopmentSetupServiceTests
{
    private const int AdminDemoPetCount = 3;
    private const int TotalDemoPetCount = 7;
    private const int TotalDemoHealthLogCount = 14;
    private const int TotalDemoScheduleItemCount = 14;
    private const int TotalDemoVisitCount = 8;

    [Fact]
    public async Task SeedDevelopmentIdentityAsync_CreatesAdminRoleAndUser_WhenMissing()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemoryDbContext(nameof(SeedDevelopmentIdentityAsync_CreatesAdminRoleAndUser_WhenMissing));
        using var userManager = CreateUserManager(dbContext);
        using var roleManager = CreateRoleManager(dbContext);

        var service = CreateService(
            dbContext,
            userManager,
            roleManager,
            new TestHostEnvironment(Environments.Development),
            Options.Create(new DevelopmentSetupOptions
            {
                AdminEmail = "admin@example.com",
                AdminPassword = "Admin123!",
                AdminDisplayName = "Development Admin"
            }));

        await service.SeedDevelopmentIdentityAsync();

        var adminUser = await userManager.FindByEmailAsync("admin@example.com");

        Assert.NotNull(adminUser);
        Assert.True(adminUser.EmailConfirmed);
        Assert.Equal("Development Admin", adminUser.DisplayName);
        Assert.True(await userManager.CheckPasswordAsync(adminUser, "Admin123!"));
        Assert.True(await userManager.IsInRoleAsync(adminUser, DevelopmentSetupService.AdminRoleName));
        Assert.True(await roleManager.RoleExistsAsync(DevelopmentSetupService.AdminRoleName));
    }

    [Fact]
    public async Task SeedDevelopmentIdentityAsync_UpdatesExistingUserAndAddsRole()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemoryDbContext(nameof(SeedDevelopmentIdentityAsync_UpdatesExistingUserAndAddsRole));
        using var userManager = CreateUserManager(dbContext);
        using var roleManager = CreateRoleManager(dbContext);

        var existingUser = new ApplicationUser
        {
            UserName = "legacy-admin",
            Email = "admin@example.com",
            EmailConfirmed = false,
            DisplayName = string.Empty
        };

        var createUserResult = await userManager.CreateAsync(existingUser, "Admin123!");
        Assert.True(createUserResult.Succeeded, string.Join(", ", createUserResult.Errors.Select(x => x.Description)));

        var service = CreateService(
            dbContext,
            userManager,
            roleManager,
            new TestHostEnvironment(Environments.Development),
            Options.Create(new DevelopmentSetupOptions
            {
                AdminEmail = "admin@example.com",
                AdminDisplayName = "Seeded Admin"
            }));

        await service.SeedDevelopmentIdentityAsync();

        var adminUser = await userManager.FindByEmailAsync("admin@example.com");

        Assert.NotNull(adminUser);
        Assert.Equal("admin@example.com", adminUser.UserName);
        Assert.True(adminUser.EmailConfirmed);
        Assert.Equal("Seeded Admin", adminUser.DisplayName);
        Assert.True(await userManager.IsInRoleAsync(adminUser, DevelopmentSetupService.AdminRoleName));
    }

    [Fact]
    public async Task SeedDevelopmentIdentityAsync_ThrowsOutsideDevelopment()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemoryDbContext(nameof(SeedDevelopmentIdentityAsync_ThrowsOutsideDevelopment));
        using var userManager = CreateUserManager(dbContext);
        using var roleManager = CreateRoleManager(dbContext);

        var service = CreateService(
            dbContext,
            userManager,
            roleManager,
            new TestHostEnvironment("Staging"),
            Options.Create(new DevelopmentSetupOptions
            {
                AdminEmail = "admin@example.com",
                AdminPassword = "Admin123!"
            }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SeedDevelopmentIdentityAsync());

        Assert.Contains("Development environment", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeedAdminIdentityAsync_CreatesAdminRoleAndUser_OutsideDevelopment()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemoryDbContext(nameof(SeedAdminIdentityAsync_CreatesAdminRoleAndUser_OutsideDevelopment));
        using var userManager = CreateUserManager(dbContext);
        using var roleManager = CreateRoleManager(dbContext);

        var service = CreateService(
            dbContext,
            userManager,
            roleManager,
            new TestHostEnvironment(Environments.Production),
            Options.Create(new DevelopmentSetupOptions
            {
                AdminEmail = "prod-admin@example.com",
                AdminPassword = "Admin123!",
                AdminDisplayName = "Production Admin"
            }));

        await service.SeedAdminIdentityAsync();

        var adminUser = await userManager.FindByEmailAsync("prod-admin@example.com");

        Assert.NotNull(adminUser);
        Assert.True(adminUser.EmailConfirmed);
        Assert.Equal("Production Admin", adminUser.DisplayName);
        Assert.True(await userManager.CheckPasswordAsync(adminUser, "Admin123!"));
        Assert.True(await userManager.IsInRoleAsync(adminUser, DevelopmentSetupService.AdminRoleName));
        Assert.True(await roleManager.RoleExistsAsync(DevelopmentSetupService.AdminRoleName));
        Assert.Empty(dbContext.Pets);
        Assert.Empty(dbContext.HealthLogs);
        Assert.Empty(dbContext.ScheduleItems);
        Assert.Empty(dbContext.Visits);
    }

    [Fact]
    public async Task SeedDemoDataAsync_CreatesAdminAndDemoRecords_OutsideDevelopment()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemoryDbContext(nameof(SeedDemoDataAsync_CreatesAdminAndDemoRecords_OutsideDevelopment));
        using var userManager = CreateUserManager(dbContext);
        using var roleManager = CreateRoleManager(dbContext);

        var service = CreateService(
            dbContext,
            userManager,
            roleManager,
            new TestHostEnvironment(Environments.Production),
            Options.Create(new DevelopmentSetupOptions
            {
                AdminEmail = "prod-admin@example.com",
                AdminPassword = "Admin123!",
                AdminDisplayName = "Production Admin",
                DemoUserPassword = "Demo123!"
            }));

        await service.SeedDemoDataAsync();

        var adminUser = await userManager.FindByEmailAsync("prod-admin@example.com");
        var satoUser = await userManager.FindByEmailAsync("demo.sato@example.com");
        var tanakaUser = await userManager.FindByEmailAsync("demo.tanaka@example.com");

        Assert.NotNull(adminUser);
        Assert.NotNull(satoUser);
        Assert.NotNull(tanakaUser);
        Assert.True(await userManager.IsInRoleAsync(adminUser, DevelopmentSetupService.AdminRoleName));
        Assert.False(await userManager.IsInRoleAsync(satoUser, DevelopmentSetupService.AdminRoleName));
        Assert.False(await userManager.IsInRoleAsync(tanakaUser, DevelopmentSetupService.AdminRoleName));
        Assert.True(await userManager.CheckPasswordAsync(satoUser, "Demo123!"));
        Assert.True(await userManager.CheckPasswordAsync(tanakaUser, "Demo123!"));
        Assert.Equal(AdminDemoPetCount, dbContext.Pets.Count(x => x.OwnerId == adminUser.Id));
        Assert.Equal(2, dbContext.Pets.Count(x => x.OwnerId == satoUser.Id));
        Assert.Equal(2, dbContext.Pets.Count(x => x.OwnerId == tanakaUser.Id));
        Assert.Equal(TotalDemoPetCount, dbContext.Pets.Count());
        Assert.Equal(TotalDemoHealthLogCount, dbContext.HealthLogs.Count());
        Assert.Equal(TotalDemoScheduleItemCount, dbContext.ScheduleItems.Count());
        Assert.Equal(TotalDemoVisitCount, dbContext.Visits.Count());
        Assert.All(dbContext.HealthLogs, x => Assert.Equal(TimeSpan.FromHours(9), x.RecordedAt.Offset));
    }

    [Fact]
    public async Task SeedDemoDataAsync_ThrowsOutsideDevelopment_WhenDemoUserPasswordMissing()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemoryDbContext(nameof(SeedDemoDataAsync_ThrowsOutsideDevelopment_WhenDemoUserPasswordMissing));
        using var userManager = CreateUserManager(dbContext);
        using var roleManager = CreateRoleManager(dbContext);

        var service = CreateService(
            dbContext,
            userManager,
            roleManager,
            new TestHostEnvironment(Environments.Production),
            Options.Create(new DevelopmentSetupOptions
            {
                AdminEmail = "prod-admin@example.com",
                AdminPassword = "Admin123!",
                AdminDisplayName = "Production Admin"
            }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SeedDemoDataAsync());

        Assert.Contains("DemoUserPassword", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeedDevelopmentDemoDataAsync_CreatesAdminAndDemoRecords_WhenMissing()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemoryDbContext(nameof(SeedDevelopmentDemoDataAsync_CreatesAdminAndDemoRecords_WhenMissing));
        using var userManager = CreateUserManager(dbContext);
        using var roleManager = CreateRoleManager(dbContext);

        var service = CreateService(
            dbContext,
            userManager,
            roleManager,
            new TestHostEnvironment(Environments.Development),
            Options.Create(new DevelopmentSetupOptions
            {
                AdminEmail = "admin@example.com",
                AdminPassword = "Admin123!",
                AdminDisplayName = "Development Admin"
            }));

        await service.SeedDevelopmentDemoDataAsync();

        var adminUser = await userManager.FindByEmailAsync("admin@example.com");

        Assert.NotNull(adminUser);
        Assert.True(await userManager.IsInRoleAsync(adminUser, DevelopmentSetupService.AdminRoleName));
        Assert.Equal(AdminDemoPetCount, dbContext.Pets.Count(x => x.OwnerId == adminUser.Id));
        Assert.Equal(TotalDemoPetCount, dbContext.Pets.Count());
        Assert.Equal(TotalDemoHealthLogCount, dbContext.HealthLogs.Count());
        Assert.Equal(TotalDemoScheduleItemCount, dbContext.ScheduleItems.Count());
        Assert.Equal(TotalDemoVisitCount, dbContext.Visits.Count());
        Assert.Contains(dbContext.Pets, x => x.OwnerId == adminUser.Id && x.Name == "まめ" && !x.IsPublic);
        Assert.Contains(dbContext.Pets, x => x.Name == "レオ" && !x.IsPublic);
        Assert.All(dbContext.HealthLogs, x => Assert.Equal(TimeSpan.FromHours(9), x.RecordedAt.Offset));
    }

    [Fact]
    public async Task SeedDevelopmentDemoDataAsync_IsIdempotent()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemoryDbContext(nameof(SeedDevelopmentDemoDataAsync_IsIdempotent));
        using var userManager = CreateUserManager(dbContext);
        using var roleManager = CreateRoleManager(dbContext);

        var service = CreateService(
            dbContext,
            userManager,
            roleManager,
            new TestHostEnvironment(Environments.Development),
            Options.Create(new DevelopmentSetupOptions
            {
                AdminEmail = "admin@example.com",
                AdminPassword = "Admin123!",
                AdminDisplayName = "Development Admin"
            }));

        await service.SeedDevelopmentDemoDataAsync();
        await service.SeedDevelopmentDemoDataAsync();

        var adminUser = await userManager.FindByEmailAsync("admin@example.com");

        Assert.NotNull(adminUser);
        Assert.Equal(AdminDemoPetCount, dbContext.Pets.Count(x => x.OwnerId == adminUser.Id));
        Assert.Equal(TotalDemoPetCount, dbContext.Pets.Count());
        Assert.Equal(TotalDemoHealthLogCount, dbContext.HealthLogs.Count());
        Assert.Equal(TotalDemoScheduleItemCount, dbContext.ScheduleItems.Count());
        Assert.Equal(TotalDemoVisitCount, dbContext.Visits.Count());
        Assert.Equal(3, userManager.Users.Count());
    }

    [Fact]
    public async Task SeedDevelopmentDemoDataAsync_ThrowsOutsideDevelopment()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemoryDbContext(nameof(SeedDevelopmentDemoDataAsync_ThrowsOutsideDevelopment));
        using var userManager = CreateUserManager(dbContext);
        using var roleManager = CreateRoleManager(dbContext);

        var service = CreateService(
            dbContext,
            userManager,
            roleManager,
            new TestHostEnvironment("Staging"),
            Options.Create(new DevelopmentSetupOptions
            {
                AdminEmail = "admin@example.com",
                AdminPassword = "Admin123!"
            }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SeedDevelopmentDemoDataAsync());

        Assert.Contains("Development environment", exception.Message, StringComparison.Ordinal);
    }

    private static DevelopmentSetupService CreateService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IHostEnvironment hostEnvironment,
        IOptions<DevelopmentSetupOptions> options)
    {
        return new DevelopmentSetupService(
            dbContext,
            userManager,
            roleManager,
            hostEnvironment,
            options,
            NullLogger<DevelopmentSetupService>.Instance);
    }

    private static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext dbContext)
    {
        var userStore = new UserStore<ApplicationUser, IdentityRole, ApplicationDbContext>(dbContext);

        return new UserManager<ApplicationUser>(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [new UserValidator<ApplicationUser>()],
            [new PasswordValidator<ApplicationUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    private static RoleManager<IdentityRole> CreateRoleManager(ApplicationDbContext dbContext)
    {
        var roleStore = new RoleStore<IdentityRole, ApplicationDbContext>(dbContext);

        return new RoleManager<IdentityRole>(
            roleStore,
            [new RoleValidator<IdentityRole>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "PetHealthManagement.Web.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

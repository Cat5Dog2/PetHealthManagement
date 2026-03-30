using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PetHealthManagement.Web.Areas.Admin.Controllers;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.Tests.Infrastructure;
using PetHealthManagement.Web.ViewModels.Admin.Users;

namespace PetHealthManagement.Web.Tests.Areas.Admin.Controllers;

public class UsersControllerTests
{
    [Fact]
    public async Task Index_ReturnsUsersWithPetCounts()
    {
        await using var dbContext = CreateDbContext();

        dbContext.Users.AddRange(
            new ApplicationUser { Id = "admin-user", UserName = "admin", DisplayName = "Admin User", Email = "admin@example.com" },
            new ApplicationUser { Id = "user-a", UserName = "userA", DisplayName = "Hanako", Email = "hanako@example.com" },
            new ApplicationUser { Id = "user-b", UserName = "userB", DisplayName = "", Email = "taro@example.com" });

        dbContext.Pets.AddRange(
            NewPet(1, "user-a", "Mugi"),
            NewPet(2, "user-a", "Sora"),
            NewPet(3, "user-b", "Koko"));

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext);
        var result = await controller.Index(page: null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminUserIndexViewModel>(viewResult.Model);

        Assert.Equal(1, model.Page);
        Assert.Equal(3, model.TotalCount);
        Assert.Collection(
            model.Users,
            x =>
            {
                Assert.Equal("Admin User", x.DisplayName);
                Assert.Equal(0, x.PetCount);
            },
            x =>
            {
                Assert.Equal("Hanako", x.DisplayName);
                Assert.Equal(2, x.PetCount);
            },
            x =>
            {
                Assert.Equal("userB", x.DisplayName);
                Assert.Equal(1, x.PetCount);
            });
    }

    [Fact]
    public async Task Index_UsesPageOne_WhenPageIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser { Id = "admin-user", UserName = "admin" });
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext);
        var result = await controller.Index(page: "abc");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminUserIndexViewModel>(viewResult.Model);
        Assert.Equal(1, model.Page);
    }

    [Fact]
    public async Task Index_ReturnsSecondPage_WhenMoreThanPageSize()
    {
        await using var dbContext = CreateDbContext();

        for (var index = 1; index <= 11; index++)
        {
            dbContext.Users.Add(new ApplicationUser
            {
                Id = $"user-{index:D2}",
                UserName = $"user{index:D2}",
                DisplayName = $"User {index:D2}",
                Email = $"user{index:D2}@example.com"
            });
        }

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext);
        var result = await controller.Index(page: "2");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminUserIndexViewModel>(viewResult.Model);

        Assert.Equal(2, model.Page);
        Assert.Single(model.Users);
        Assert.Equal(11, model.TotalCount);
    }

    [Fact]
    public async Task Delete_RedirectsToAdminUsers_WhenDeletionSucceeds()
    {
        await using var dbContext = CreateDbContext();
        var deletionService = new FakeUserDataDeletionService { NextResult = true };
        var logger = new TestLogger<UsersController>();
        var controller = BuildController(dbContext, deletionService, logger);

        var result = await controller.Delete("user-a");

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Admin/Users", redirectResult.Url);
        Assert.Equal("user-a", deletionService.DeletedUserId);
        Assert.Collection(
            logger.Entries,
            entry =>
            {
                Assert.Equal(LogLevel.Information, entry.LogLevel);
                Assert.Equal(ApplicationOperationLogging.Operations.AdminDeleteUser, entry.Properties["Operation"]);
                Assert.Equal("admin-user", entry.Properties["ActorUserId"]);
                Assert.Equal("User", entry.Properties["TargetType"]);
                Assert.Equal("user-a", entry.Properties["TargetId"]);
            },
            entry =>
            {
                Assert.Equal(LogLevel.Information, entry.LogLevel);
                Assert.Equal(ApplicationOperationLogging.Operations.AdminDeleteUser, entry.Properties["Operation"]);
                Assert.Equal("admin-user", entry.Properties["ActorUserId"]);
                Assert.Equal("User", entry.Properties["TargetType"]);
                Assert.Equal("user-a", entry.Properties["TargetId"]);
            });
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenUserDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var logger = new TestLogger<UsersController>();
        var controller = BuildController(dbContext, new FakeUserDataDeletionService { NextResult = false }, logger);

        var result = await controller.Delete("missing-user");

        Assert.IsType<NotFoundResult>(result);
        Assert.Collection(
            logger.Entries,
            entry =>
            {
                Assert.Equal(LogLevel.Information, entry.LogLevel);
                Assert.Equal(ApplicationOperationLogging.Operations.AdminDeleteUser, entry.Properties["Operation"]);
                Assert.Equal("admin-user", entry.Properties["ActorUserId"]);
                Assert.Equal("User", entry.Properties["TargetType"]);
                Assert.Equal("missing-user", entry.Properties["TargetId"]);
            },
            entry =>
            {
                Assert.Equal(LogLevel.Warning, entry.LogLevel);
                Assert.Equal(ApplicationOperationLogging.Operations.AdminDeleteUser, entry.Properties["Operation"]);
                Assert.Equal("admin-user", entry.Properties["ActorUserId"]);
                Assert.Equal("User", entry.Properties["TargetType"]);
                Assert.Equal("missing-user", entry.Properties["TargetId"]);
            });
    }

    private static UsersController BuildController(
        ApplicationDbContext dbContext,
        IUserDataDeletionService? userDataDeletionService = null,
        ILogger<UsersController>? logger = null)
    {
        var controller = new UsersController(
            dbContext,
            userDataDeletionService ?? new FakeUserDataDeletionService(),
            logger ?? NullLogger<UsersController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "admin-user"),
                            new Claim(ClaimTypes.Role, "Admin")
                        ],
                        "TestAuth"))
            }
        };

        return controller;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"admin-users-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Pet NewPet(int id, string ownerId, string name)
    {
        var now = DateTimeOffset.UtcNow;
        return new Pet
        {
            Id = id,
            OwnerId = ownerId,
            Name = name,
            SpeciesCode = "DOG",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private sealed class FakeUserDataDeletionService : IUserDataDeletionService
    {
        public bool NextResult { get; init; }

        public string? DeletedUserId { get; private set; }

        public Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            DeletedUserId = userId;
            return Task.FromResult(NextResult);
        }
    }
}

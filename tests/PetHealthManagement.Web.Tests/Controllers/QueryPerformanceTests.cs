using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PetHealthManagement.Web.Areas.Admin.Controllers;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Controllers;

public class QueryPerformanceTests
{
    [Fact]
    public async Task AdminUsersIndex_ExecutesBoundedQueryCount_ForPageAndPetCounts()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var interceptor = new QueryCountingDbCommandInterceptor();
        await using var dbContext = await CreateDbContextAsync(connection, interceptor);

        dbContext.Users.AddRange(
            new ApplicationUser { Id = "admin-user", UserName = "admin-user", DisplayName = "Admin User" },
            new ApplicationUser { Id = "owner-user", UserName = "owner-user", DisplayName = "Owner User" },
            new ApplicationUser { Id = "other-user", UserName = "other-user", Email = "other@example.com" });

        dbContext.Pets.AddRange(
            NewPet(1, "owner-user", "Mugi", true),
            NewPet(2, "owner-user", "Sora", false),
            NewPet(3, "other-user", "Koko", true));

        await dbContext.SaveChangesAsync();

        interceptor.Reset();

        var controller = BuildUsersController(dbContext);
        var result = await controller.Index(page: null);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(2, interceptor.ExecutedCommandCount);
    }

    private static async Task<ApplicationDbContext> CreateDbContextAsync(
        SqliteConnection connection,
        QueryCountingDbCommandInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private static UsersController BuildUsersController(ApplicationDbContext dbContext)
    {
        var controller = new UsersController(
            dbContext,
            new FakeUserDataDeletionService(),
            NullLogger<UsersController>.Instance);
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

    private static Pet NewPet(int id, string ownerId, string name, bool isPublic)
    {
        var now = DateTimeOffset.UtcNow;
        return new Pet
        {
            Id = id,
            OwnerId = ownerId,
            Name = name,
            SpeciesCode = "DOG",
            IsPublic = isPublic,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private sealed class FakeUserDataDeletionService : IUserDataDeletionService
    {
        public Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            _ = userId;
            _ = cancellationToken;
            return Task.FromResult(true);
        }
    }
}

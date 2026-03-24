using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Integration;

public class AuthorizationIntegrationTests
{
    [Fact]
    public async Task AnonymousUser_ProtectedPage_RedirectsToLoginWithReturnUrl()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);

        using var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync("/MyPage");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/Identity/Account/Login", response.Headers.Location!.AbsolutePath);

        var query = QueryHelpers.ParseQuery(response.Headers.Location.Query);
        Assert.Equal("/MyPage", query["ReturnUrl"].ToString());
    }

    [Fact]
    public async Task LoggedInNonAdmin_AdminUsers_Returns403()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            dbContext.Users.Add(new ApplicationUser
            {
                Id = "owner-user",
                UserName = "owner-user"
            });

            return Task.CompletedTask;
        });

        using var client = factory.CreateAuthenticatedClient("owner-user");

        var response = await client.GetAsync("/Admin/Users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("403 Forbidden", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_DeleteUser_RemovesRelatedData_AndRedirectsToAdminUsers()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedAdminDeleteScenario(dbContext);
            return Task.CompletedTask;
        });

        using var client = factory.CreateAuthenticatedClient("admin-user", roles: ["Admin"]);
        var antiforgeryRequestData = await factory.CreateAntiforgeryRequestDataAsync("admin-user", roles: ["Admin"]);
        client.DefaultRequestHeaders.Add("Cookie", antiforgeryRequestData.CookieHeaderValue);

        using var indexResponse = await client.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);

        using var response = await client.PostAsync(
            "/Admin/Users/Delete/delete-user",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [antiforgeryRequestData.FormFieldName] = antiforgeryRequestData.RequestToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/Admin/Users", response.Headers.Location!.OriginalString);

        var remainingState = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            DeletedUserExists = await dbContext.Users.AnyAsync(x => x.Id == "delete-user"),
            DeletedPetExists = await dbContext.Pets.AnyAsync(x => x.OwnerId == "delete-user"),
            DeletedHealthLogExists = await dbContext.HealthLogs.AnyAsync(x => x.Pet.OwnerId == "delete-user"),
            DeletedVisitExists = await dbContext.Visits.AnyAsync(x => x.Pet.OwnerId == "delete-user"),
            DeletedScheduleItemExists = await dbContext.ScheduleItems.AnyAsync(x => x.Pet.OwnerId == "delete-user"),
            KeptUserExists = await dbContext.Users.AnyAsync(x => x.Id == "keep-user")
        });

        Assert.False(remainingState.DeletedUserExists);
        Assert.False(remainingState.DeletedPetExists);
        Assert.False(remainingState.DeletedHealthLogExists);
        Assert.False(remainingState.DeletedVisitExists);
        Assert.False(remainingState.DeletedScheduleItemExists);
        Assert.True(remainingState.KeptUserExists);
    }

    [Fact]
    public async Task Admin_CannotAccessOtherUsersHiddenResources()
    {
        var petPhotoImageId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedHiddenResourceScenario(dbContext, petPhotoImageId);
            return Task.CompletedTask;
        });

        using var client = factory.CreateAuthenticatedClient("admin-user", roles: ["Admin"]);

        foreach (var path in new[]
                 {
                     "/Pets/Details/1",
                     "/HealthLogs/Details/10",
                     "/ScheduleItems/Details/20",
                     "/Visits/Details/30",
                     $"/images/{petPhotoImageId:D}"
                 })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("404 Not Found", html, StringComparison.Ordinal);
        }
    }

    private static void SeedAdminDeleteScenario(ApplicationDbContext dbContext)
    {
        var now = new DateTimeOffset(2026, 3, 24, 9, 0, 0, TimeSpan.FromHours(9));

        dbContext.Users.AddRange(
            new ApplicationUser
            {
                Id = "delete-user",
                UserName = "delete-user",
                Email = "delete@example.com"
            },
            new ApplicationUser
            {
                Id = "keep-user",
                UserName = "keep-user",
                Email = "keep@example.com"
            });

        dbContext.Pets.AddRange(
            new Pet
            {
                Id = 1,
                OwnerId = "delete-user",
                Name = "Mugi",
                SpeciesCode = "DOG",
                IsPublic = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Pet
            {
                Id = 2,
                OwnerId = "keep-user",
                Name = "Sora",
                SpeciesCode = "CAT",
                IsPublic = true,
                CreatedAt = now,
                UpdatedAt = now
            });

        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });

        dbContext.Visits.Add(new Visit
        {
            Id = 30,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 24),
            CreatedAt = now,
            UpdatedAt = now
        });

        dbContext.ScheduleItems.Add(new ScheduleItem
        {
            Id = 20,
            PetId = 1,
            DueDate = new DateTime(2026, 3, 28),
            Type = ScheduleItemTypeCatalog.Other,
            Title = "Checkup",
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static void SeedHiddenResourceScenario(ApplicationDbContext dbContext, Guid petPhotoImageId)
    {
        var now = new DateTimeOffset(2026, 3, 24, 9, 0, 0, TimeSpan.FromHours(9));

        dbContext.Users.Add(new ApplicationUser
        {
            Id = "owner-user",
            UserName = "owner-user",
            Email = "owner@example.com"
        });

        dbContext.Pets.Add(new Pet
        {
            Id = 1,
            OwnerId = "owner-user",
            Name = "Mugi",
            SpeciesCode = "DOG",
            IsPublic = false,
            PhotoImageId = petPhotoImageId,
            CreatedAt = now,
            UpdatedAt = now
        });

        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });

        dbContext.ScheduleItems.Add(new ScheduleItem
        {
            Id = 20,
            PetId = 1,
            DueDate = new DateTime(2026, 3, 28),
            Type = ScheduleItemTypeCatalog.Other,
            Title = "Vaccination",
            CreatedAt = now,
            UpdatedAt = now
        });

        dbContext.Visits.Add(new Visit
        {
            Id = 30,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 24),
            ClinicName = "Central Clinic",
            CreatedAt = now,
            UpdatedAt = now
        });

        dbContext.ImageAssets.Add(new ImageAsset
        {
            ImageId = petPhotoImageId,
            StorageKey = "images/private-pet.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 256,
            OwnerId = "owner-user",
            Category = "PetPhoto",
            Status = ImageAssetStatus.Ready,
            CreatedAt = now
        });
    }
}

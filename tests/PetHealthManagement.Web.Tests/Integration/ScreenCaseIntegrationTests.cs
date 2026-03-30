using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Tests.Infrastructure;
using PetHealthManagement.Web.ViewModels.Account;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PetHealthManagement.Web.Tests.Integration;

public class ScreenCaseIntegrationTests
{
    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 3, 30, 9, 0, 0, TimeSpan.FromHours(9));

    [Fact]
    public async Task MyPage_ShowsOwnedPetsAndNavigationLinks()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedUsers(dbContext);
            dbContext.Pets.AddRange(
                new Pet
                {
                    Id = 1,
                    OwnerId = "owner-user",
                    Name = "Mugi",
                    SpeciesCode = "DOG",
                    IsPublic = false,
                    CreatedAt = SeedTimestamp,
                    UpdatedAt = SeedTimestamp
                },
                new Pet
                {
                    Id = 2,
                    OwnerId = "other-user",
                    Name = "Other User Pet",
                    SpeciesCode = "CAT",
                    IsPublic = true,
                    CreatedAt = SeedTimestamp,
                    UpdatedAt = SeedTimestamp
                });

            return Task.CompletedTask;
        });

        using var client = factory.CreateAuthenticatedClient("owner-user");
        using var response = await client.GetAsync("/MyPage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await ReadDecodedHtmlAsync(response);
        Assert.Contains("owner@example.com", html, StringComparison.Ordinal);
        Assert.Contains("/images/default/avatar-placeholder.svg", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Account/EditProfile?returnUrl=%2FMyPage\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Account/Delete?returnUrl=%2FMyPage\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Pets/Create?returnUrl=%2FMyPage\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Pets/Details/1?returnUrl=%2FMyPage\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/HealthLogs?petId=1\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/ScheduleItems?petId=1\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Visits?petId=1\"", html, StringComparison.Ordinal);
        Assert.Contains("Mugi", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Other User Pet", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditProfile_GetAndPost_PreserveReturnUrl_AndPersistAvatar()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedUsers(dbContext);
            return Task.CompletedTask;
        });

        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = factory.CreateAuthenticatedClient("owner-user");
        client.DefaultRequestHeaders.Add("Cookie", antiforgery.CookieHeaderValue);

        using (var getResponse = await client.GetAsync("/Account/EditProfile?returnUrl=%2FPets%3Fpage%3D2"))
        {
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var html = await ReadDecodedHtmlAsync(getResponse);
            Assert.Contains("name=\"returnUrl\" value=\"/Pets?page=2\"", html, StringComparison.Ordinal);
            Assert.Contains("href=\"/Pets?page=2\"", html, StringComparison.Ordinal);
            Assert.Contains("/images/default/avatar-placeholder.svg", html, StringComparison.Ordinal);
        }

        using (var postContent = CreateEditProfileContent(
                   antiforgery,
                   displayName: "Updated Owner",
                   returnUrl: "/Pets?page=2",
                   avatarFile: NewUploadFile("avatar.png", "image/png", CreatePngBytes(96, 96))))
        using (var postResponse = await client.PostAsync("/Account/EditProfile", postContent))
        {
            Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
            Assert.Equal("/Pets?page=2", postResponse.Headers.Location?.OriginalString);
        }

        var persistedState = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var user = await dbContext.Users.SingleAsync(x => x.Id == "owner-user");
            var asset = await dbContext.ImageAssets.SingleAsync(x => x.Category == "Avatar");

            return new
            {
                user.DisplayName,
                user.AvatarImageId,
                asset.ImageId,
                asset.ContentType
            };
        });

        Assert.Equal("Updated Owner", persistedState.DisplayName);
        Assert.Equal(persistedState.ImageId, persistedState.AvatarImageId);

        using (var ownerImageResponse = await client.GetAsync($"/images/{persistedState.ImageId:D}"))
        {
            Assert.Equal(HttpStatusCode.OK, ownerImageResponse.StatusCode);
            Assert.Equal(persistedState.ContentType, ownerImageResponse.Content.Headers.ContentType?.MediaType);
            Assert.NotEmpty(await ownerImageResponse.Content.ReadAsByteArrayAsync());
        }

        using var otherClient = factory.CreateAuthenticatedClient("other-user");
        using var otherImageResponse = await otherClient.GetAsync($"/images/{persistedState.ImageId:D}");
        Assert.Equal(HttpStatusCode.NotFound, otherImageResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_GetAndPost_DeleteOwnedData_AndChallengeFutureRequests()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedUsers(dbContext);
            dbContext.Pets.Add(new Pet
            {
                Id = 1,
                OwnerId = "owner-user",
                Name = "Mugi",
                SpeciesCode = "DOG",
                IsPublic = false,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp
            });
            dbContext.HealthLogs.Add(new HealthLog
            {
                Id = 10,
                PetId = 1,
                RecordedAt = SeedTimestamp,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp
            });
            dbContext.Visits.Add(new Visit
            {
                Id = 20,
                PetId = 1,
                VisitDate = new DateTime(2026, 3, 30),
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp
            });
            dbContext.ScheduleItems.Add(new ScheduleItem
            {
                Id = 30,
                PetId = 1,
                DueDate = new DateTime(2026, 3, 31),
                Type = ScheduleItemTypeCatalog.Other,
                Title = "Checkup",
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp
            });

            return Task.CompletedTask;
        });

        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = factory.CreateAuthenticatedClient("owner-user");
        client.DefaultRequestHeaders.Add("Cookie", antiforgery.CookieHeaderValue);

        using (var getResponse = await client.GetAsync("/Account/Delete?returnUrl=%2FPets%3Fpage%3D2"))
        {
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var html = await ReadDecodedHtmlAsync(getResponse);
            Assert.Contains("name=\"returnUrl\" value=\"/Pets?page=2\"", html, StringComparison.Ordinal);
            Assert.Contains("href=\"/Pets?page=2\"", html, StringComparison.Ordinal);
            Assert.Contains("owner@example.com", html, StringComparison.Ordinal);
        }

        using (var deleteContent = CreateDeleteAccountContent(antiforgery, "/Pets?page=2"))
        using (var deleteResponse = await client.PostAsync("/Account/DeleteConfirmed", deleteContent))
        {
            Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
            Assert.Equal("/", deleteResponse.Headers.Location?.OriginalString);
        }

        var deletedState = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            UserExists = await dbContext.Users.AnyAsync(x => x.Id == "owner-user"),
            PetExists = await dbContext.Pets.AnyAsync(x => x.OwnerId == "owner-user"),
            HealthLogExists = await dbContext.HealthLogs.AnyAsync(x => x.Pet.OwnerId == "owner-user"),
            VisitExists = await dbContext.Visits.AnyAsync(x => x.Pet.OwnerId == "owner-user"),
            ScheduleItemExists = await dbContext.ScheduleItems.AnyAsync(x => x.Pet.OwnerId == "owner-user")
        });

        Assert.False(deletedState.UserExists);
        Assert.False(deletedState.PetExists);
        Assert.False(deletedState.HealthLogExists);
        Assert.False(deletedState.VisitExists);
        Assert.False(deletedState.ScheduleItemExists);

        using var myPageResponse = await client.GetAsync("/MyPage");
        Assert.Equal(HttpStatusCode.Redirect, myPageResponse.StatusCode);
        Assert.Equal("/Identity/Account/Login", myPageResponse.Headers.Location?.AbsolutePath);

        var redirectQuery = QueryHelpers.ParseQuery(myPageResponse.Headers.Location?.Query ?? string.Empty);
        Assert.Equal("/MyPage", redirectQuery["ReturnUrl"].ToString());
    }

    [Theory]
    [InlineData(400, "400 Bad Request")]
    [InlineData(403, "403 Forbidden")]
    [InlineData(404, "404 Not Found")]
    [InlineData(500, "500 Internal Server Error")]
    public async Task ErrorPages_ReturnExpectedStatusCodes_AndHomeLinks(int statusCode, string heading)
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);

        using var client = factory.CreateAnonymousClient();
        using var response = await client.GetAsync($"/Error/{statusCode}");

        Assert.Equal((HttpStatusCode)statusCode, response.StatusCode);

        var html = await ReadDecodedHtmlAsync(response);
        Assert.Contains(heading, html, StringComparison.Ordinal);
        Assert.Contains("href=\"/\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteActions_WithoutAntiforgeryToken_ReturnBadRequest_AndKeepData()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedUsers(dbContext, includeAdmin: true, includeTargetUser: true);
            dbContext.Pets.Add(new Pet
            {
                Id = 1,
                OwnerId = "owner-user",
                Name = "Mugi",
                SpeciesCode = "DOG",
                IsPublic = false,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp
            });
            dbContext.HealthLogs.Add(new HealthLog
            {
                Id = 10,
                PetId = 1,
                RecordedAt = SeedTimestamp,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp
            });

            return Task.CompletedTask;
        });

        using var ownerClient = factory.CreateAuthenticatedClient("owner-user");

        using (var petDeleteResponse = await ownerClient.PostAsync(
                   "/Pets/Delete/1",
                   new FormUrlEncodedContent(
                   [
                       new KeyValuePair<string, string>("returnUrl", "/MyPage")
                   ])))
        {
            Assert.Equal(HttpStatusCode.BadRequest, petDeleteResponse.StatusCode);
        }

        using (var healthLogDeleteResponse = await ownerClient.PostAsync(
                   "/HealthLogs/Delete/10",
                   new FormUrlEncodedContent(
                   [
                       new KeyValuePair<string, string>("petId", "1"),
                       new KeyValuePair<string, string>("page", "1"),
                       new KeyValuePair<string, string>("returnUrl", "/HealthLogs?petId=1")
                   ])))
        {
            Assert.Equal(HttpStatusCode.BadRequest, healthLogDeleteResponse.StatusCode);
        }

        using var adminClient = factory.CreateAuthenticatedClient("admin-user", roles: ["Admin"]);
        using (var adminDeleteResponse = await adminClient.PostAsync(
                   "/Admin/Users/Delete/target-user",
                   new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>())))
        {
            Assert.Equal(HttpStatusCode.BadRequest, adminDeleteResponse.StatusCode);
        }

        var remainingState = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            PetExists = await dbContext.Pets.AnyAsync(x => x.Id == 1),
            HealthLogExists = await dbContext.HealthLogs.AnyAsync(x => x.Id == 10),
            TargetUserExists = await dbContext.Users.AnyAsync(x => x.Id == "target-user")
        });

        Assert.True(remainingState.PetExists);
        Assert.True(remainingState.HealthLogExists);
        Assert.True(remainingState.TargetUserExists);
    }

    private static void SeedUsers(
        ApplicationDbContext dbContext,
        bool includeAdmin = false,
        bool includeTargetUser = false)
    {
        dbContext.Users.AddRange(
            new ApplicationUser
            {
                Id = "owner-user",
                UserName = "owner-user",
                DisplayName = "Owner Display",
                Email = "owner@example.com"
            },
            new ApplicationUser
            {
                Id = "other-user",
                UserName = "other-user",
                DisplayName = "Other Display",
                Email = "other@example.com"
            });

        if (includeAdmin)
        {
            dbContext.Users.Add(new ApplicationUser
            {
                Id = "admin-user",
                UserName = "admin-user",
                DisplayName = "Admin Display",
                Email = "admin@example.com"
            });
        }

        if (includeTargetUser)
        {
            dbContext.Users.Add(new ApplicationUser
            {
                Id = "target-user",
                UserName = "target-user",
                DisplayName = "Target Display",
                Email = "target@example.com"
            });
        }
    }

    private static MultipartFormDataContent CreateEditProfileContent(
        AntiforgeryRequestData antiforgeryRequestData,
        string displayName,
        string returnUrl,
        UploadFile? avatarFile = null)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(antiforgeryRequestData.RequestToken), antiforgeryRequestData.FormFieldName);
        content.Add(new StringContent(displayName), nameof(EditProfileViewModel.DisplayName));
        content.Add(new StringContent(returnUrl), "returnUrl");

        if (avatarFile is not null)
        {
            var fileContent = new ByteArrayContent(avatarFile.Content);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(avatarFile.ContentType);
            content.Add(fileContent, nameof(EditProfileViewModel.AvatarFile), avatarFile.FileName);
        }

        return content;
    }

    private static FormUrlEncodedContent CreateDeleteAccountContent(
        AntiforgeryRequestData antiforgeryRequestData,
        string returnUrl)
    {
        return new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>(antiforgeryRequestData.FormFieldName, antiforgeryRequestData.RequestToken),
            new KeyValuePair<string, string>("returnUrl", returnUrl)
        ]);
    }

    private static UploadFile NewUploadFile(string fileName, string contentType, byte[] content)
        => new(fileName, contentType, content);

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(32, 128, 224));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static async Task<string> ReadDecodedHtmlAsync(HttpResponseMessage response)
    {
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private sealed record UploadFile(string FileName, string ContentType, byte[] Content);
}

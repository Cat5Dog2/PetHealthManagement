using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Tests.Infrastructure;
using PetHealthManagement.Web.ViewModels.Pets;
using PetHealthManagement.Web.ViewModels.Visits;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PetHealthManagement.Web.Tests.Integration;

[Trait("CiTier", "Critical")]
public class ImageFlowIntegrationTests
{
    [Fact]
    public async Task VisitImageFlow_UploadServeDelete_StaysConsistentAcrossAuthorizationBoundaries()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnerAndOtherUser(dbContext, includePetForOtherUser: false);
            dbContext.Pets.Add(new Pet
            {
                Id = 1,
                OwnerId = "owner-user",
                Name = "Mugi",
                SpeciesCode = "DOG",
                IsPublic = false,
                CreatedAt = VisitSeedTimestamp,
                UpdatedAt = VisitSeedTimestamp
            });

            return Task.CompletedTask;
        });

        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var ownerClient = CreateAuthenticatedClientWithAntiforgery(factory, antiforgery, "owner-user");
        using (var createContent = CreateVisitCreateContent(
                   antiforgery,
                   petId: 1,
                   NewUploadFile("visit.png", "image/png", CreatePngBytes(96, 96))))
        using (var createResponse = await ownerClient.PostAsync("/Visits/Create", createContent))
        {
            Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
            Assert.Equal("/Visits?petId=1", createResponse.Headers.Location?.OriginalString);
        }

        var persistedState = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var visit = await dbContext.Visits.SingleAsync();
            var visitImage = await dbContext.VisitImages.SingleAsync();
            var imageAsset = await dbContext.ImageAssets.SingleAsync(x => x.Category == "Visit");

            return new
            {
                visit.Id,
                visitImage.ImageId,
                imageAsset.ContentType
            };
        });

        using (var ownerImageResponse = await ownerClient.GetAsync($"/images/{persistedState.ImageId:D}"))
        {
            Assert.Equal(HttpStatusCode.OK, ownerImageResponse.StatusCode);
            Assert.Equal(persistedState.ContentType, ownerImageResponse.Content.Headers.ContentType?.MediaType);
            var cacheControl = ownerImageResponse.Headers.CacheControl?.ToString() ?? string.Empty;
            Assert.Contains("private", cacheControl, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no-store", cacheControl, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("inline", ownerImageResponse.Content.Headers.ContentDisposition?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.True(ownerImageResponse.Headers.TryGetValues("X-Content-Type-Options", out var nosniffValues));
            Assert.Contains("nosniff", nosniffValues.Single(), StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(await ownerImageResponse.Content.ReadAsByteArrayAsync());
        }

        using (var otherClient = factory.CreateAuthenticatedClient("other-user"))
        using (var otherImageResponse = await otherClient.GetAsync($"/images/{persistedState.ImageId:D}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, otherImageResponse.StatusCode);
        }

        using (var deleteContent = CreateVisitDeleteContent(antiforgery, returnUrl: "/Visits?petId=1"))
        using (var deleteResponse = await ownerClient.PostAsync($"/Visits/Delete/{persistedState.Id}", deleteContent))
        {
            Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
            Assert.Equal("/Visits?petId=1", deleteResponse.Headers.Location?.OriginalString);
        }

        using (var deletedImageResponse = await ownerClient.GetAsync($"/images/{persistedState.ImageId:D}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, deletedImageResponse.StatusCode);
        }

        var deletedState = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            VisitCount = await dbContext.Visits.CountAsync(),
            VisitImageCount = await dbContext.VisitImages.CountAsync(),
            VisitAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "Visit")
        });

        Assert.Equal(0, deletedState.VisitCount);
        Assert.Equal(0, deletedState.VisitImageCount);
        Assert.Equal(0, deletedState.VisitAssetCount);
    }

    [Fact]
    public async Task PetPhotoFlow_UploadServeDelete_StaysConsistentAcrossAuthorizationBoundaries()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnerAndOtherUser(dbContext, includePetForOtherUser: false);
            return Task.CompletedTask;
        });

        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var ownerClient = CreateAuthenticatedClientWithAntiforgery(factory, antiforgery, "owner-user");
        using (var createContent = CreatePetCreateContent(
                   antiforgery,
                   name: "Mugi",
                   speciesCode: "DOG",
                   isPublic: false,
                   NewUploadFile("pet.png", "image/png", CreatePngBytes(96, 96))))
        using (var createResponse = await ownerClient.PostAsync("/Pets/Create", createContent))
        {
            Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
            Assert.Equal("/MyPage", createResponse.Headers.Location?.OriginalString);
        }

        var persistedState = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var pet = await dbContext.Pets.SingleAsync();
            var imageAsset = await dbContext.ImageAssets.SingleAsync(x => x.Category == "PetPhoto");

            return new
            {
                pet.Id,
                ImageId = imageAsset.ImageId,
                imageAsset.ContentType
            };
        });

        using (var ownerImageResponse = await ownerClient.GetAsync($"/images/{persistedState.ImageId:D}"))
        {
            Assert.Equal(HttpStatusCode.OK, ownerImageResponse.StatusCode);
            Assert.Equal(persistedState.ContentType, ownerImageResponse.Content.Headers.ContentType?.MediaType);
            Assert.NotEmpty(await ownerImageResponse.Content.ReadAsByteArrayAsync());
        }

        using (var otherClient = factory.CreateAuthenticatedClient("other-user"))
        using (var otherImageResponse = await otherClient.GetAsync($"/images/{persistedState.ImageId:D}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, otherImageResponse.StatusCode);
        }

        using (var deleteContent = CreatePetDeleteContent(antiforgery))
        using (var deleteResponse = await ownerClient.PostAsync($"/Pets/Delete/{persistedState.Id}", deleteContent))
        {
            Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
            Assert.Equal("/MyPage", deleteResponse.Headers.Location?.OriginalString);
        }

        using (var deletedImageResponse = await ownerClient.GetAsync($"/images/{persistedState.ImageId:D}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, deletedImageResponse.StatusCode);
        }

        var deletedState = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            PetCount = await dbContext.Pets.CountAsync(),
            PetAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "PetPhoto")
        });

        Assert.Equal(0, deletedState.PetCount);
        Assert.Equal(0, deletedState.PetAssetCount);
    }

    private static readonly DateTimeOffset VisitSeedTimestamp =
        new(2026, 3, 24, 9, 0, 0, TimeSpan.FromHours(9));

    private static HttpClient CreateAuthenticatedClientWithAntiforgery(
        IntegrationTestWebApplicationFactory factory,
        AntiforgeryRequestData antiforgeryRequestData,
        string userId)
    {
        var client = factory.CreateAuthenticatedClient(userId);
        client.DefaultRequestHeaders.Add("Cookie", antiforgeryRequestData.CookieHeaderValue);
        return client;
    }

    private static MultipartFormDataContent CreateVisitCreateContent(
        AntiforgeryRequestData antiforgeryRequestData,
        int petId,
        params UploadFile[] files)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(antiforgeryRequestData.RequestToken), antiforgeryRequestData.FormFieldName);
        content.Add(new StringContent(petId.ToString()), nameof(VisitEditViewModel.PetId));
        content.Add(new StringContent("2026-03-24"), nameof(VisitEditViewModel.VisitDate));
        content.Add(new StringContent(string.Empty), "returnUrl");

        foreach (var file in files)
        {
            var fileContent = new ByteArrayContent(file.Content);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
            content.Add(fileContent, nameof(VisitEditViewModel.NewFiles), file.FileName);
        }

        return content;
    }

    private static FormUrlEncodedContent CreateVisitDeleteContent(
        AntiforgeryRequestData antiforgeryRequestData,
        string returnUrl)
    {
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [antiforgeryRequestData.FormFieldName] = antiforgeryRequestData.RequestToken,
            ["petId"] = "1",
            ["returnUrl"] = returnUrl
        });
    }

    private static MultipartFormDataContent CreatePetCreateContent(
        AntiforgeryRequestData antiforgeryRequestData,
        string name,
        string speciesCode,
        bool isPublic,
        params UploadFile[] files)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(antiforgeryRequestData.RequestToken), antiforgeryRequestData.FormFieldName);
        content.Add(new StringContent(name), nameof(PetEditViewModel.Name));
        content.Add(new StringContent(speciesCode), nameof(PetEditViewModel.SpeciesCode));
        content.Add(new StringContent(string.Empty), "returnUrl");
        content.Add(new StringContent(isPublic ? "true" : "false"), nameof(PetEditViewModel.IsPublic));

        foreach (var file in files)
        {
            var fileContent = new ByteArrayContent(file.Content);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
            content.Add(fileContent, nameof(PetEditViewModel.PhotoFile), file.FileName);
        }

        return content;
    }

    private static FormUrlEncodedContent CreatePetDeleteContent(AntiforgeryRequestData antiforgeryRequestData)
    {
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [antiforgeryRequestData.FormFieldName] = antiforgeryRequestData.RequestToken,
            ["returnUrl"] = string.Empty
        });
    }

    private static void SeedOwnerAndOtherUser(ApplicationDbContext dbContext, bool includePetForOtherUser)
    {
        dbContext.Users.AddRange(
            new ApplicationUser
            {
                Id = "owner-user",
                UserName = "owner-user",
                Email = "owner@example.com"
            },
            new ApplicationUser
            {
                Id = "other-user",
                UserName = "other-user",
                Email = "other@example.com"
            });

        if (!includePetForOtherUser)
        {
            return;
        }

        dbContext.Pets.Add(new Pet
        {
            Id = 2,
            OwnerId = "other-user",
            Name = "Sora",
            SpeciesCode = "CAT",
            IsPublic = false,
            CreatedAt = VisitSeedTimestamp,
            UpdatedAt = VisitSeedTimestamp
        });
    }

    private static UploadFile NewUploadFile(string fileName, string contentType, byte[] content)
        => new(fileName, contentType, content);

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, Color.CadetBlue);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private sealed record UploadFile(string FileName, string ContentType, byte[] Content);
}

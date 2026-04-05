using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.Tests.Infrastructure;
using PetHealthManagement.Web.ViewModels.Visits;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PetHealthManagement.Web.Tests.Integration;

[Trait("CiTier", "Critical")]
public class ImageUploadIntegrationTests
{
    [Fact]
    public async Task VisitCreate_WithSpoofedImage_ReturnsValidationError_AndDoesNotPersistData()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnedPetScenario(dbContext);
            return Task.CompletedTask;
        });

        var antiforgeryRequestData = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateAuthenticatedClientWithAntiforgery(factory, antiforgeryRequestData);
        using var content = CreateVisitCreateContent(
            antiforgeryRequestData,
            NewUploadFile("spoofed.png", "image/png", Encoding.UTF8.GetBytes("not-an-image")));

        using var response = await client.PostAsync("/Visits/Create", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);
        Assert.Contains(ImageUploadErrorMessages.UnsupportedFormat, decodedHtml, StringComparison.Ordinal);

        var state = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            VisitCount = await dbContext.Visits.CountAsync(),
            VisitImageCount = await dbContext.VisitImages.CountAsync(),
            VisitAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "Visit")
        });

        Assert.Equal(0, state.VisitCount);
        Assert.Equal(0, state.VisitImageCount);
        Assert.Equal(0, state.VisitAssetCount);
    }

    [Fact]
    public async Task VisitCreate_WithFileLargerThan2Mb_ReturnsValidationError_AndDoesNotPersistData()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnedPetScenario(dbContext);
            return Task.CompletedTask;
        });

        var antiforgeryRequestData = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateAuthenticatedClientWithAntiforgery(factory, antiforgeryRequestData);
        using var content = CreateVisitCreateContent(
            antiforgeryRequestData,
            NewUploadFile("too-large.png", "image/png", new byte[(2 * 1024 * 1024) + 1]));

        using var response = await client.PostAsync("/Visits/Create", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);
        Assert.Contains(ImageUploadErrorMessages.FileTooLarge, decodedHtml, StringComparison.Ordinal);

        var state = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            VisitCount = await dbContext.Visits.CountAsync(),
            VisitImageCount = await dbContext.VisitImages.CountAsync(),
            VisitAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "Visit")
        });

        Assert.Equal(0, state.VisitCount);
        Assert.Equal(0, state.VisitImageCount);
        Assert.Equal(0, state.VisitAssetCount);
    }

    [Fact]
    public async Task VisitCreate_WithImageWiderThan4096Px_ReturnsValidationError_AndDoesNotPersistData()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnedPetScenario(dbContext);
            return Task.CompletedTask;
        });

        var antiforgeryRequestData = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateAuthenticatedClientWithAntiforgery(factory, antiforgeryRequestData);
        using var content = CreateVisitCreateContent(
            antiforgeryRequestData,
            NewUploadFile("too-wide.png", "image/png", CreatePngBytes(width: 5000, height: 32)));

        using var response = await client.PostAsync("/Visits/Create", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);
        Assert.Contains(ImageUploadErrorMessages.DimensionsExceeded, decodedHtml, StringComparison.Ordinal);

        var state = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            VisitCount = await dbContext.Visits.CountAsync(),
            VisitImageCount = await dbContext.VisitImages.CountAsync(),
            VisitAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "Visit")
        });

        Assert.Equal(0, state.VisitCount);
        Assert.Equal(0, state.VisitImageCount);
        Assert.Equal(0, state.VisitAssetCount);
    }

    [Fact]
    public async Task VisitCreate_WithMoreThan10Images_ReturnsValidationError_AndDoesNotPersistData()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnedPetScenario(dbContext);
            return Task.CompletedTask;
        });

        var antiforgeryRequestData = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateAuthenticatedClientWithAntiforgery(factory, antiforgeryRequestData);
        using var content = CreateVisitCreateContent(
            antiforgeryRequestData,
            Enumerable.Range(1, 11)
                .Select(index => NewUploadFile($"visit-{index}.png", "image/png", CreatePngBytes(16, 16)))
                .ToArray());

        using var response = await client.PostAsync("/Visits/Create", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);
        Assert.Contains(ImageUploadErrorMessages.TooManyAttachments, decodedHtml, StringComparison.Ordinal);

        var state = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            VisitCount = await dbContext.Visits.CountAsync(),
            VisitImageCount = await dbContext.VisitImages.CountAsync(),
            VisitAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "Visit")
        });

        Assert.Equal(0, state.VisitCount);
        Assert.Equal(0, state.VisitImageCount);
        Assert.Equal(0, state.VisitAssetCount);
    }

    [Fact]
    public async Task VisitCreate_WhenUserStorageWouldExceed100Mb_ReturnsValidationError_AndDoesNotPersistVisitImages()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnedPetScenario(dbContext);
            dbContext.ImageAssets.Add(new ImageAsset
            {
                ImageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                StorageKey = "images/existing-avatar.jpg",
                ContentType = "image/jpeg",
                SizeBytes = (100 * 1024 * 1024) - 10,
                OwnerId = "owner-user",
                Category = "Avatar",
                Status = ImageAssetStatus.Ready,
                CreatedAt = new DateTimeOffset(2026, 3, 24, 9, 0, 0, TimeSpan.FromHours(9))
            });

            return Task.CompletedTask;
        });

        var antiforgeryRequestData = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateAuthenticatedClientWithAntiforgery(factory, antiforgeryRequestData);
        using var content = CreateVisitCreateContent(
            antiforgeryRequestData,
            NewUploadFile("visit.png", "image/png", CreatePngBytes(64, 64)));

        using var response = await client.PostAsync("/Visits/Create", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);
        Assert.Contains(ImageUploadErrorMessages.TotalStorageExceeded, decodedHtml, StringComparison.Ordinal);

        var state = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            VisitCount = await dbContext.Visits.CountAsync(),
            VisitImageCount = await dbContext.VisitImages.CountAsync(),
            VisitAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "Visit"),
            ExistingAssetCount = await dbContext.ImageAssets.CountAsync(x => x.OwnerId == "owner-user")
        });

        Assert.Equal(0, state.VisitCount);
        Assert.Equal(0, state.VisitImageCount);
        Assert.Equal(0, state.VisitAssetCount);
        Assert.Equal(1, state.ExistingAssetCount);
    }

    [Fact]
    public async Task VisitCreate_WhenMultipartRequestExceedsConfiguredLimit_ReturnsError400_AndDoesNotPersistData()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnedPetScenario(dbContext);
            return Task.CompletedTask;
        });

        var antiforgeryRequestData = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateAuthenticatedClientWithAntiforgery(factory, antiforgeryRequestData);
        using var content = CreateVisitCreateContent(
            antiforgeryRequestData,
            NewUploadFile(
                "request-too-large.png",
                "image/png",
                new byte[(int)UploadRequestLimits.MaxMultipartRequestBodySizeBytes + 1024]));

        using var response = await client.PostAsync("/Visits/Create", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var state = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            VisitCount = await dbContext.Visits.CountAsync(),
            VisitImageCount = await dbContext.VisitImages.CountAsync(),
            VisitAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "Visit")
        });

        Assert.Equal(0, state.VisitCount);
        Assert.Equal(0, state.VisitImageCount);
        Assert.Equal(0, state.VisitAssetCount);
    }

    private static HttpClient CreateAuthenticatedClientWithAntiforgery(
        IntegrationTestWebApplicationFactory factory,
        AntiforgeryRequestData antiforgeryRequestData)
    {
        var client = factory.CreateAuthenticatedClient("owner-user");
        client.DefaultRequestHeaders.Add("Cookie", antiforgeryRequestData.CookieHeaderValue);
        return client;
    }

    private static MultipartFormDataContent CreateVisitCreateContent(
        AntiforgeryRequestData antiforgeryRequestData,
        params UploadFile[] files)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(antiforgeryRequestData.RequestToken), antiforgeryRequestData.FormFieldName);
        content.Add(new StringContent("1"), nameof(VisitEditViewModel.PetId));
        content.Add(new StringContent("2026-03-24"), nameof(VisitEditViewModel.VisitDate));
        content.Add(new StringContent(string.Empty), nameof(VisitEditViewModel.ReturnUrl));

        foreach (var file in files)
        {
            var fileContent = new ByteArrayContent(file.Content);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
            content.Add(fileContent, nameof(VisitEditViewModel.NewFiles), file.FileName);
        }

        return content;
    }

    private static void SeedOwnedPetScenario(ApplicationDbContext dbContext)
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
            CreatedAt = now,
            UpdatedAt = now
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

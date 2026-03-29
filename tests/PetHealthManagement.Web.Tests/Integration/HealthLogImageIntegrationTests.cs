using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.Tests.Infrastructure;
using PetHealthManagement.Web.ViewModels.HealthLogs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PetHealthManagement.Web.Tests.Integration;

public class HealthLogImageIntegrationTests
{
    [Fact]
    public async Task HealthLogCreate_WithSpoofedImage_ReturnsValidationError_AndDoesNotPersistData()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnedPetScenario(dbContext);
            return Task.CompletedTask;
        });

        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateAuthenticatedClientWithAntiforgery(factory, antiforgery);
        using var content = CreateHealthLogCreateContent(
            antiforgery,
            NewUploadFile("spoofed.png", "image/png", Encoding.UTF8.GetBytes("not-an-image")));

        using var response = await client.PostAsync("/HealthLogs/Create", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(ImageUploadErrorMessages.UnsupportedFormat, html, StringComparison.Ordinal);

        var state = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            HealthLogCount = await dbContext.HealthLogs.CountAsync(),
            HealthLogImageCount = await dbContext.HealthLogImages.CountAsync(),
            HealthLogAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "HealthLog")
        });

        Assert.Equal(0, state.HealthLogCount);
        Assert.Equal(0, state.HealthLogImageCount);
        Assert.Equal(0, state.HealthLogAssetCount);
    }

    [Fact]
    public async Task HealthLogCreate_WithFileLargerThan2Mb_ReturnsValidationError_AndDoesNotPersistData()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnedPetScenario(dbContext);
            return Task.CompletedTask;
        });

        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateAuthenticatedClientWithAntiforgery(factory, antiforgery);
        using var content = CreateHealthLogCreateContent(
            antiforgery,
            NewUploadFile("too-large.png", "image/png", new byte[(2 * 1024 * 1024) + 1]));

        using var response = await client.PostAsync("/HealthLogs/Create", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(ImageUploadErrorMessages.FileTooLarge, html, StringComparison.Ordinal);

        var state = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            HealthLogCount = await dbContext.HealthLogs.CountAsync(),
            HealthLogImageCount = await dbContext.HealthLogImages.CountAsync(),
            HealthLogAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "HealthLog")
        });

        Assert.Equal(0, state.HealthLogCount);
        Assert.Equal(0, state.HealthLogImageCount);
        Assert.Equal(0, state.HealthLogAssetCount);
    }

    [Fact]
    public async Task HealthLogCreate_WithImageWiderThan4096Px_ReturnsValidationError_AndDoesNotPersistData()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnedPetScenario(dbContext);
            return Task.CompletedTask;
        });

        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateAuthenticatedClientWithAntiforgery(factory, antiforgery);
        using var content = CreateHealthLogCreateContent(
            antiforgery,
            NewUploadFile("too-wide.png", "image/png", CreatePngBytes(width: 5000, height: 32)));

        using var response = await client.PostAsync("/HealthLogs/Create", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(ImageUploadErrorMessages.DimensionsExceeded, html, StringComparison.Ordinal);

        var state = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            HealthLogCount = await dbContext.HealthLogs.CountAsync(),
            HealthLogImageCount = await dbContext.HealthLogImages.CountAsync(),
            HealthLogAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "HealthLog")
        });

        Assert.Equal(0, state.HealthLogCount);
        Assert.Equal(0, state.HealthLogImageCount);
        Assert.Equal(0, state.HealthLogAssetCount);
    }

    [Fact]
    public async Task HealthLogCreate_WithMoreThan10Images_ReturnsValidationError_AndDoesNotPersistData()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnedPetScenario(dbContext);
            return Task.CompletedTask;
        });

        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateAuthenticatedClientWithAntiforgery(factory, antiforgery);
        using var content = CreateHealthLogCreateContent(
            antiforgery,
            Enumerable.Range(1, 11)
                .Select(index => NewUploadFile($"healthlog-{index}.png", "image/png", CreatePngBytes(16, 16)))
                .ToArray());

        using var response = await client.PostAsync("/HealthLogs/Create", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(ImageUploadErrorMessages.TooManyAttachments, html, StringComparison.Ordinal);

        var state = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            HealthLogCount = await dbContext.HealthLogs.CountAsync(),
            HealthLogImageCount = await dbContext.HealthLogImages.CountAsync(),
            HealthLogAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "HealthLog")
        });

        Assert.Equal(0, state.HealthLogCount);
        Assert.Equal(0, state.HealthLogImageCount);
        Assert.Equal(0, state.HealthLogAssetCount);
    }

    [Fact]
    public async Task HealthLogCreate_WhenUserStorageWouldExceed100Mb_ReturnsValidationError_AndDoesNotPersistData()
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
                CreatedAt = SeedTimestamp
            });

            return Task.CompletedTask;
        });

        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateAuthenticatedClientWithAntiforgery(factory, antiforgery);
        using var content = CreateHealthLogCreateContent(
            antiforgery,
            NewUploadFile("healthlog.png", "image/png", CreatePngBytes(64, 64)));

        using var response = await client.PostAsync("/HealthLogs/Create", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(ImageUploadErrorMessages.TotalStorageExceeded, html, StringComparison.Ordinal);

        var state = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            HealthLogCount = await dbContext.HealthLogs.CountAsync(),
            HealthLogImageCount = await dbContext.HealthLogImages.CountAsync(),
            HealthLogAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "HealthLog"),
            ExistingAssetCount = await dbContext.ImageAssets.CountAsync(x => x.OwnerId == "owner-user")
        });

        Assert.Equal(0, state.HealthLogCount);
        Assert.Equal(0, state.HealthLogImageCount);
        Assert.Equal(0, state.HealthLogAssetCount);
        Assert.Equal(1, state.ExistingAssetCount);
    }

    [Fact]
    public async Task HealthLogImageFlow_CreateEditDelete_StaysConsistentAcrossAuthorizationBoundaries()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnedPetScenario(dbContext);
            dbContext.Users.Add(new ApplicationUser
            {
                Id = "other-user",
                UserName = "other-user",
                Email = "other@example.com"
            });

            return Task.CompletedTask;
        });

        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var ownerClient = CreateAuthenticatedClientWithAntiforgery(factory, antiforgery);
        using (var createContent = CreateHealthLogCreateContent(
                   antiforgery,
                   NewUploadFile("log-1.png", "image/png", CreatePngBytes(96, 96)),
                   NewUploadFile("log-2.png", "image/png", CreatePngBytes(80, 80))))
        using (var createResponse = await ownerClient.PostAsync("/HealthLogs/Create", createContent))
        {
            Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
            Assert.Equal("/HealthLogs?petId=1", createResponse.Headers.Location?.OriginalString);
        }

        var createdState = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var healthLog = await dbContext.HealthLogs.SingleAsync();
            healthLog.RowVersion ??= [1, 0, 0, 0];
            await dbContext.SaveChangesAsync();

            var imageIds = await dbContext.HealthLogImages
                .Where(x => x.HealthLogId == healthLog.Id)
                .OrderBy(x => x.SortOrder)
                .Select(x => x.ImageId)
                .ToListAsync();
            var contentTypes = await dbContext.ImageAssets
                .Where(x => imageIds.Contains(x.ImageId))
                .ToDictionaryAsync(x => x.ImageId, x => x.ContentType);

            return new
            {
                healthLog.Id,
                RowVersion = Convert.ToBase64String(healthLog.RowVersion),
                ImageIds = imageIds,
                ContentTypes = contentTypes
            };
        });

        Assert.Equal(2, createdState.ImageIds.Count);

        using (var ownerImageResponse = await ownerClient.GetAsync($"/images/{createdState.ImageIds[0]:D}"))
        {
            Assert.Equal(HttpStatusCode.OK, ownerImageResponse.StatusCode);
            Assert.Equal(createdState.ContentTypes[createdState.ImageIds[0]], ownerImageResponse.Content.Headers.ContentType?.MediaType);
            var cacheControl = ownerImageResponse.Headers.CacheControl?.ToString() ?? string.Empty;
            Assert.Contains("private", cacheControl, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no-store", cacheControl, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(await ownerImageResponse.Content.ReadAsByteArrayAsync());
        }

        using (var otherClient = factory.CreateAuthenticatedClient("other-user"))
        using (var otherImageResponse = await otherClient.GetAsync($"/images/{createdState.ImageIds[0]:D}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, otherImageResponse.StatusCode);
        }

        using (var editContent = CreateHealthLogEditContent(
                   antiforgery,
                   createdState.Id,
                   createdState.RowVersion,
                   createdState.ImageIds[0],
                   NewUploadFile("log-3.png", "image/png", CreatePngBytes(72, 72))))
        using (var editResponse = await ownerClient.PostAsync($"/HealthLogs/Edit/{createdState.Id}", editContent))
        {
            Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
            Assert.Equal($"/HealthLogs/Details/{createdState.Id}", editResponse.Headers.Location?.OriginalString);
        }

        var editedState = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var healthLog = await dbContext.HealthLogs.SingleAsync();
            var imageIds = await dbContext.HealthLogImages
                .Where(x => x.HealthLogId == healthLog.Id)
                .OrderBy(x => x.SortOrder)
                .Select(x => x.ImageId)
                .ToListAsync();

            return new
            {
                healthLog.Note,
                ImageIds = imageIds
            };
        });

        Assert.Equal("updated note", editedState.Note);
        Assert.Equal(2, editedState.ImageIds.Count);
        Assert.DoesNotContain(createdState.ImageIds[0], editedState.ImageIds);

        using (var deletedImageResponse = await ownerClient.GetAsync($"/images/{createdState.ImageIds[0]:D}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, deletedImageResponse.StatusCode);
        }

        var newImageId = editedState.ImageIds.Single(x => !createdState.ImageIds.Contains(x));
        using (var newImageResponse = await ownerClient.GetAsync($"/images/{newImageId:D}"))
        {
            Assert.Equal(HttpStatusCode.OK, newImageResponse.StatusCode);
            Assert.NotEmpty(await newImageResponse.Content.ReadAsByteArrayAsync());
        }

        using (var deleteContent = CreateHealthLogDeleteContent(antiforgery, "/HealthLogs?petId=1"))
        using (var deleteResponse = await ownerClient.PostAsync($"/HealthLogs/Delete/{createdState.Id}", deleteContent))
        {
            Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
            Assert.Equal("/HealthLogs?petId=1", deleteResponse.Headers.Location?.OriginalString);
        }

        foreach (var imageId in editedState.ImageIds)
        {
            using var deletedResponse = await ownerClient.GetAsync($"/images/{imageId:D}");
            Assert.Equal(HttpStatusCode.NotFound, deletedResponse.StatusCode);
        }

        var deletedState = await factory.ExecuteDbContextAsync(async dbContext => new
        {
            HealthLogCount = await dbContext.HealthLogs.CountAsync(),
            HealthLogImageCount = await dbContext.HealthLogImages.CountAsync(),
            HealthLogAssetCount = await dbContext.ImageAssets.CountAsync(x => x.Category == "HealthLog")
        });

        Assert.Equal(0, deletedState.HealthLogCount);
        Assert.Equal(0, deletedState.HealthLogImageCount);
        Assert.Equal(0, deletedState.HealthLogAssetCount);
    }

    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 3, 24, 9, 0, 0, TimeSpan.FromHours(9));

    private static HttpClient CreateAuthenticatedClientWithAntiforgery(
        IntegrationTestWebApplicationFactory factory,
        AntiforgeryRequestData antiforgeryRequestData)
    {
        var client = factory.CreateAuthenticatedClient("owner-user");
        client.DefaultRequestHeaders.Add("Cookie", antiforgeryRequestData.CookieHeaderValue);
        return client;
    }

    private static MultipartFormDataContent CreateHealthLogCreateContent(
        AntiforgeryRequestData antiforgeryRequestData,
        params UploadFile[] files)
    {
        return CreateHealthLogEditPayload(
            antiforgeryRequestData,
            note: "created note",
            deleteImageId: null,
            files);
    }

    private static MultipartFormDataContent CreateHealthLogEditContent(
        AntiforgeryRequestData antiforgeryRequestData,
        int healthLogId,
        string rowVersion,
        Guid deleteImageId,
        params UploadFile[] files)
    {
        return CreateHealthLogEditPayload(
            antiforgeryRequestData,
            note: "updated note",
            deleteImageId,
            files,
            healthLogId,
            rowVersion);
    }

    private static MultipartFormDataContent CreateHealthLogEditPayload(
        AntiforgeryRequestData antiforgeryRequestData,
        string note,
        Guid? deleteImageId,
        UploadFile[] files,
        int? healthLogId = null,
        string? rowVersion = null)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(antiforgeryRequestData.RequestToken), antiforgeryRequestData.FormFieldName);
        content.Add(new StringContent("1"), nameof(HealthLogEditViewModel.PetId));
        content.Add(new StringContent("2026-03-24T09:30"), nameof(HealthLogEditViewModel.RecordedAt));
        content.Add(new StringContent("5.4"), nameof(HealthLogEditViewModel.WeightKg));
        content.Add(new StringContent("120"), nameof(HealthLogEditViewModel.FoodAmountGram));
        content.Add(new StringContent("30"), nameof(HealthLogEditViewModel.WalkMinutes));
        content.Add(new StringContent("good"), nameof(HealthLogEditViewModel.StoolCondition));
        content.Add(new StringContent(note), nameof(HealthLogEditViewModel.Note));
        content.Add(new StringContent(string.Empty), "returnUrl");

        if (healthLogId.HasValue)
        {
            content.Add(new StringContent(healthLogId.Value.ToString(CultureInfo.InvariantCulture)), nameof(HealthLogEditViewModel.HealthLogId));
        }

        if (!string.IsNullOrWhiteSpace(rowVersion))
        {
            content.Add(new StringContent(rowVersion), nameof(HealthLogEditViewModel.RowVersion));
        }

        if (deleteImageId.HasValue)
        {
            content.Add(new StringContent(deleteImageId.Value.ToString("D")), nameof(HealthLogEditViewModel.DeleteImageIds));
        }

        foreach (var file in files)
        {
            var fileContent = new ByteArrayContent(file.Content);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
            content.Add(fileContent, nameof(HealthLogEditViewModel.NewFiles), file.FileName);
        }

        return content;
    }

    private static FormUrlEncodedContent CreateHealthLogDeleteContent(
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

    private static void SeedOwnedPetScenario(ApplicationDbContext dbContext)
    {
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
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
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

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

namespace PetHealthManagement.Web.Tests.Integration;

public class UploadRateLimitIntegrationTests
{
    [Fact]
    public async Task VisitCreate_WhenSameUserExceedsUploadRateLimit_Returns429_AndDoesNotAffectAnotherUser()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedVisitOwners(dbContext);
            return Task.CompletedTask;
        });

        var ownerAntiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var ownerClient = CreateAuthenticatedClientWithAntiforgery(factory, ownerAntiforgery, "owner-user");

        for (var attempt = 0; attempt < UploadRateLimiting.PermitLimit; attempt++)
        {
            using var content = CreateVisitCreateContent(
                ownerAntiforgery,
                petId: 1,
                NewUploadFile("spoofed.png", "image/png", Encoding.UTF8.GetBytes("not-an-image")));

            using var response = await ownerClient.PostAsync("/Visits/Create", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using (var limitedContent = CreateVisitCreateContent(
                   ownerAntiforgery,
                   petId: 1,
                   NewUploadFile("spoofed.png", "image/png", Encoding.UTF8.GetBytes("not-an-image"))))
        using (var limitedResponse = await ownerClient.PostAsync("/Visits/Create", limitedContent))
        {
            Assert.Equal((HttpStatusCode)429, limitedResponse.StatusCode);
            Assert.True(limitedResponse.Headers.TryGetValues("Retry-After", out _));

            var html = await limitedResponse.Content.ReadAsStringAsync();
            Assert.Contains("429 Too Many Requests", html, StringComparison.Ordinal);
        }

        var otherAntiforgery = await factory.CreateAntiforgeryRequestDataAsync("other-user");
        using var otherClient = CreateAuthenticatedClientWithAntiforgery(factory, otherAntiforgery, "other-user");
        using (var otherContent = CreateVisitCreateContent(
                   otherAntiforgery,
                   petId: 2,
                   NewUploadFile("spoofed.png", "image/png", Encoding.UTF8.GetBytes("not-an-image"))))
        using (var otherResponse = await otherClient.PostAsync("/Visits/Create", otherContent))
        {
            Assert.Equal(HttpStatusCode.OK, otherResponse.StatusCode);

            var html = await otherResponse.Content.ReadAsStringAsync();
            var decodedHtml = WebUtility.HtmlDecode(html);
            Assert.Contains(ImageUploadErrorMessages.UnsupportedFormat, decodedHtml, StringComparison.Ordinal);
        }

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
        content.Add(new StringContent(string.Empty), nameof(VisitEditViewModel.ReturnUrl));

        foreach (var file in files)
        {
            var fileContent = new ByteArrayContent(file.Content);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
            content.Add(fileContent, nameof(VisitEditViewModel.NewFiles), file.FileName);
        }

        return content;
    }

    private static void SeedVisitOwners(ApplicationDbContext dbContext)
    {
        var now = new DateTimeOffset(2026, 3, 24, 9, 0, 0, TimeSpan.FromHours(9));

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

        dbContext.Pets.AddRange(
            new Pet
            {
                Id = 1,
                OwnerId = "owner-user",
                Name = "Mugi",
                SpeciesCode = "DOG",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Pet
            {
                Id = 2,
                OwnerId = "other-user",
                Name = "Sora",
                SpeciesCode = "CAT",
                CreatedAt = now,
                UpdatedAt = now
            });
    }

    private static UploadFile NewUploadFile(string fileName, string contentType, byte[] content)
        => new(fileName, contentType, content);

    private sealed record UploadFile(string FileName, string ContentType, byte[] Content);
}

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Integration;

// 共通エラーページは元のリクエストの HTTP メソッドのまま再実行されるため、POST の後でも表示できることを確かめる
public class ErrorPageForPostIntegrationTests
{
    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 9, 27, 9, 0, 0, TimeSpan.FromHours(9));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostToUnknownUrl_Returns404Page(bool withAntiforgeryToken)
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(SeedOwnerWithPet);
        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateOwnerClient(factory, antiforgery);

        using var response = await client.PostAsync(
            "/no-such-path",
            CreateForm(withAntiforgeryToken ? antiforgery : null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertErrorPageAsync(response, "404 Not Found", "指定されたページは見つかりませんでした。");
    }

    [Fact]
    public async Task FormPostWithoutAntiforgeryToken_Returns400Page_AndKeepsData()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(SeedOwnerWithPet);
        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateOwnerClient(factory, antiforgery);

        using var response = await client.PostAsync("/Pets/Delete/1", CreateForm(antiforgery: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorPageAsync(response, "400 Bad Request", "入力内容が不正です。");
        Assert.True(await factory.ExecuteDbContextAsync(dbContext => dbContext.Pets.AnyAsync(x => x.Id == 1)));
    }

    // エラーページが本文を読むと、読み取りに失敗する本文の後で正しいページを出せない
    [Fact]
    public async Task OversizedMultipartPost_Returns400Page()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(SeedOwnerWithPet);
        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateOwnerClient(factory, antiforgery);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(antiforgery.RequestToken), antiforgery.FormFieldName);
        content.Add(new StringContent("1"), "PetId");
        var oversizedFile = new ByteArrayContent(new byte[(int)UploadRequestLimits.MaxMultipartRequestBodySizeBytes + 1024]);
        oversizedFile.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(oversizedFile, "NewFiles", "too-large.png");

        using var response = await client.PostAsync("/Visits/Create", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorPageAsync(response, "400 Bad Request", "入力内容が不正です。");
    }

    // メソッド不一致（405）は、存在しない扱いの 404 にそろえる
    [Theory]
    [InlineData("POST", "/images/3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    [InlineData("POST", "/css/site.css")]
    [InlineData("PUT", "/MyPage")]
    public async Task WrongHttpMethod_Returns404Page_WithoutAllowHeader(string method, string path)
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(SeedOwnerWithPet);
        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateOwnerClient(factory, antiforgery);

        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = CreateForm(antiforgery)
        };
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(response.Content.Headers.Allow);
        await AssertErrorPageAsync(response, "404 Not Found", "指定されたページは見つかりませんでした。");
    }

    // テストはビルド出力から起動するため、MapStaticAssets が GET/HEAD のみの fallback（{**path:file}）を追加する。
    // 発行したアプリ（本番）にはこの fallback がなく、POST 専用の URL への GET は 405、存在しない URL への POST は
    // 404 のまま POST で再実行される。fallback を外し、本番と同じルーティングでも確かめる。
    [Theory]
    [InlineData("GET", "/Pets/Delete/1")]
    [InlineData("POST", "/no-such-path")]
    public async Task PublishedAppRouting_Returns404Page_WithoutAllowHeader(string method, string path)
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        Assert.Contains(GetEndpoints(factory), IsStaticAssetFallback);
        var publishedAppFactory = factory.WithWebHostBuilder(
            builder => builder.UseSetting("ReloadStaticAssetsAtRuntime", "false"));
        Assert.DoesNotContain(GetEndpoints(publishedAppFactory), IsStaticAssetFallback);
        using var client = publishedAppFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "POST" ? CreateForm(antiforgery: null) : null
        };
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(response.Content.Headers.Allow);
        await AssertErrorPageAsync(response, "404 Not Found", "指定されたページは見つかりませんでした。");
    }

    [Theory]
    [InlineData(405, HttpStatusCode.NotFound, "404 Not Found")]
    [InlineData(415, HttpStatusCode.BadRequest, "400 Bad Request")]
    [InlineData(503, HttpStatusCode.InternalServerError, "500 Internal Server Error")]
    [InlineData(200, HttpStatusCode.NotFound, "404 Not Found")]
    public async Task ErrorPage_ShowsNearestPage_ForCodesWithoutOwnPage(
        int requestedStatusCode,
        HttpStatusCode expectedStatusCode,
        string expectedHeading)
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);
        using var client = factory.CreateAnonymousClient();

        using var response = await client.GetAsync($"/Error/{requestedStatusCode}");

        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Contains($"<h1 class=\"text-danger\">{expectedHeading}</h1>", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RateLimitedPostWithoutAntiforgeryToken_Shows429Page()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(SeedOwnerWithPet);
        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = CreateOwnerClient(factory, antiforgery);

        // レート制限は antiforgery の検証より前に数えるため、トークンなしの送信でも枠を使い切る
        for (var attempt = 0; attempt < UploadRateLimiting.PermitLimit; attempt++)
        {
            using var rejected = await client.PostAsync("/Visits/Create", CreateForm(antiforgery: null));
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }

        using var response = await client.PostAsync("/Visits/Create", CreateForm(antiforgery: null));

        Assert.Equal((HttpStatusCode)429, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Retry-After", out _));
        Assert.Contains("少し時間をおいてから、もう一度お試しください。", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static HttpClient CreateOwnerClient(IntegrationTestWebApplicationFactory factory, AntiforgeryRequestData antiforgery)
    {
        var client = factory.CreateAuthenticatedClient("owner-user");
        client.DefaultRequestHeaders.Add("Cookie", antiforgery.CookieHeaderValue);
        return client;
    }

    private static FormUrlEncodedContent CreateForm(AntiforgeryRequestData? antiforgery)
    {
        var fields = new List<KeyValuePair<string, string>> { new("returnUrl", "/MyPage") };
        if (antiforgery is not null)
        {
            fields.Add(new KeyValuePair<string, string>(antiforgery.FormFieldName, antiforgery.RequestToken));
        }

        return new FormUrlEncodedContent(fields);
    }

    private static IReadOnlyList<Endpoint> GetEndpoints(WebApplicationFactory<Program> factory)
    {
        return factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
    }

    private static bool IsStaticAssetFallback(Endpoint endpoint)
    {
        return endpoint is RouteEndpoint { RoutePattern.RawText: "{**path:file}" };
    }

    private static async Task AssertErrorPageAsync(HttpResponseMessage response, string heading, string message)
    {
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains($"<h1 class=\"text-danger\">{heading}</h1>", html, StringComparison.Ordinal);
        Assert.Contains(message, html, StringComparison.Ordinal);
        Assert.Contains("href=\"/\"", html, StringComparison.Ordinal);
    }

    private static Task SeedOwnerWithPet(ApplicationDbContext dbContext)
    {
        dbContext.Users.Add(new ApplicationUser
        {
            Id = "owner-user",
            UserName = "owner-user",
            DisplayName = "Owner Display",
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

        return Task.CompletedTask;
    }
}

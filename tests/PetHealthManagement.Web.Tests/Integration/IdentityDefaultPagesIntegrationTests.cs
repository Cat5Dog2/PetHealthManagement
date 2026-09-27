using System.Net;
using System.Text.RegularExpressions;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Integration;

public class IdentityDefaultPagesIntegrationTests
{
    private const string LogoutPath = "/Identity/Account/Logout";

    // AddDefaultIdentity が同梱する Identity UI の既定ページのうち、アプリのコントローラーで置き換えていないもの。
    // ログイン・登録・アカウント管理（Index/Email/ChangePassword/TwoFactorAuthentication/PersonalData）は
    // 自前のコントローラーが応答するため含めない（ScreenCaseIntegrationTests で確認している）。
    public static TheoryData<string> UnusedIdentityUiPages =>
    [
        "/Identity/Account/AccessDenied",
        "/Identity/Account/ConfirmEmail",
        "/Identity/Account/ConfirmEmailChange",
        "/Identity/Account/ExternalLogin",
        "/Identity/Account/ForgotPassword",
        "/Identity/Account/ForgotPasswordConfirmation",
        "/Identity/Account/Lockout",
        "/Identity/Account/LoginWith2fa",
        "/Identity/Account/LoginWithRecoveryCode",
        "/Identity/Account/RegisterConfirmation",
        "/Identity/Account/ResendEmailConfirmation",
        "/Identity/Account/ResetPassword",
        "/Identity/Account/ResetPasswordConfirmation",
        "/Identity/Account/Manage/DeletePersonalData",
        "/Identity/Account/Manage/Disable2fa",
        "/Identity/Account/Manage/DownloadPersonalData",
        "/Identity/Account/Manage/EnableAuthenticator",
        "/Identity/Account/Manage/ExternalLogins",
        "/Identity/Account/Manage/GenerateRecoveryCodes",
        "/Identity/Account/Manage/ResetAuthenticator",
        "/Identity/Account/Manage/SetPassword",
        "/Identity/Account/Manage/ShowRecoveryCodes"
    ];

    [Theory]
    [MemberData(nameof(UnusedIdentityUiPages))]
    public async Task UnusedIdentityUiPage_ReturnsNotFound(string path)
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(SeedOwnerAsync);
        using var anonymousClient = factory.CreateAnonymousClient();
        using var ownerClient = factory.CreateAuthenticatedClient("owner-user");

        using var anonymousResponse = await anonymousClient.GetAsync(path);
        using var ownerResponse = await ownerClient.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ownerResponse.StatusCode);
        Assert.Contains("404 Not Found", await ownerResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // POST の応答コードは存在しない URL への POST 共通の扱いになるため、ここではページの処理が動かないことを確かめる
    [Fact]
    public async Task UnusedIdentityUiPostHandlers_NoLongerChangeTheAccount()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(SeedOwnerAsync);
        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = factory.CreateAuthenticatedClient("owner-user");
        client.DefaultRequestHeaders.Add("Cookie", antiforgery.CookieHeaderValue);

        // 既定の SetPassword はパスワード未設定のユーザーにパスワードを設定し、
        // 既定の DeletePersonalData はペットや画像を残したまま Identity ユーザーだけを削除する
        using var setPasswordResponse = await client.PostAsync(
            "/Identity/Account/Manage/SetPassword",
            CreateForm(
                antiforgery.FormFieldName,
                antiforgery.RequestToken,
                ("Input.NewPassword", "Pa$$w0rd!"),
                ("Input.ConfirmPassword", "Pa$$w0rd!")));
        using var deletePersonalDataResponse = await client.PostAsync(
            "/Identity/Account/Manage/DeletePersonalData",
            CreateForm(antiforgery.FormFieldName, antiforgery.RequestToken));

        Assert.True((int)setPasswordResponse.StatusCode >= 400, $"SetPassword returned {(int)setPasswordResponse.StatusCode}.");
        Assert.True((int)deletePersonalDataResponse.StatusCode >= 400, $"DeletePersonalData returned {(int)deletePersonalDataResponse.StatusCode}.");

        var owner = await factory.ExecuteDbContextAsync(dbContext => dbContext.Users.FindAsync("owner-user").AsTask());
        Assert.NotNull(owner);
        Assert.Null(owner.PasswordHash);
    }

    [Fact]
    public async Task LogoutPage_HasNoGetScreen()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(SeedOwnerAsync);
        using var ownerClient = factory.CreateAuthenticatedClient("owner-user");

        using var response = await ownerClient.GetAsync(LogoutPath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Logout_Post_SignsOutCookieUserAndRedirectsHome()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);
        using var client = factory.CreateAnonymousClient();
        await RegisterAsync(client);

        using var myPageResponse = await client.GetAsync("/MyPage");
        Assert.Equal(HttpStatusCode.OK, myPageResponse.StatusCode);
        var myPageHtml = await myPageResponse.Content.ReadAsStringAsync();
        Assert.Contains($"action=\"{LogoutPath}?returnUrl=%2F\"", myPageHtml, StringComparison.Ordinal);

        using var logoutResponse = await client.PostAsync(
            $"{LogoutPath}?returnUrl=%2F",
            CreateForm("__RequestVerificationToken", ExtractAntiforgeryToken(myPageHtml)));

        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
        Assert.Equal("/", logoutResponse.Headers.Location?.OriginalString);
        Assert.Contains(
            logoutResponse.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith("__Host-PetHealthManagement.Auth=;", StringComparison.Ordinal));

        using var afterLogoutResponse = await client.GetAsync("/MyPage");
        Assert.Equal(HttpStatusCode.Redirect, afterLogoutResponse.StatusCode);
        Assert.Equal("/Identity/Account/Login", afterLogoutResponse.Headers.Location?.AbsolutePath);
    }

    [Theory]
    [InlineData("/Pets?page=2", "/Pets?page=2")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("//evil.example/", "/")]
    [InlineData("/\\evil.example/", "/")]
    public async Task Logout_RedirectsOnlyToLocalReturnUrl(string returnUrl, string expectedLocation)
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(SeedOwnerAsync);
        var antiforgery = await factory.CreateAntiforgeryRequestDataAsync("owner-user");
        using var client = factory.CreateAuthenticatedClient("owner-user");
        client.DefaultRequestHeaders.Add("Cookie", antiforgery.CookieHeaderValue);

        using var response = await client.PostAsync(
            LogoutPath,
            CreateForm(antiforgery.FormFieldName, antiforgery.RequestToken, ("returnUrl", returnUrl)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Logout_WithoutAntiforgeryToken_Returns400()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(SeedOwnerAsync);
        using var client = factory.CreateAuthenticatedClient("owner-user");

        using var response = await client.PostAsync(LogoutPath, new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HomeAndHeader_LinkToAppLoginAndRegister()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);
        using var client = factory.CreateAnonymousClient();

        var html = await client.GetStringAsync("/");

        // トップのボタンとヘッダーの両方にリンクがある
        Assert.Equal(2, Regex.Matches(html, "href=\"/Identity/Account/Login\"").Count);
        Assert.Equal(2, Regex.Matches(html, "href=\"/Identity/Account/Register\"").Count);
    }

    private static Task SeedOwnerAsync(ApplicationDbContext dbContext)
    {
        dbContext.Users.Add(new ApplicationUser
        {
            Id = "owner-user",
            UserName = "owner-user",
            DisplayName = "Owner Display",
            Email = "owner@example.com"
        });

        return Task.CompletedTask;
    }

    private static async Task RegisterAsync(HttpClient client)
    {
        using var registerPage = await client.GetAsync("/Identity/Account/Register");
        var token = ExtractAntiforgeryToken(await registerPage.Content.ReadAsStringAsync());

        using var response = await client.PostAsync(
            "/Identity/Account/Register",
            CreateForm(
                "__RequestVerificationToken",
                token,
                ("Email", "logout-owner@example.com"),
                ("Password", "Pa$$w0rd!"),
                ("ConfirmPassword", "Pa$$w0rd!")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static FormUrlEncodedContent CreateForm(
        string tokenFieldName,
        string token,
        params (string Name, string Value)[] fields)
    {
        return new FormUrlEncodedContent(
            fields
                .Select(x => new KeyValuePair<string, string>(x.Name, x.Value))
                .Append(new KeyValuePair<string, string>(tokenFieldName, token)));
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            @"<input\b[^>]*\bname=""__RequestVerificationToken""[^>]*\bvalue=""([^""]+)""",
            RegexOptions.IgnoreCase);

        Assert.True(match.Success, "Could not find the antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}

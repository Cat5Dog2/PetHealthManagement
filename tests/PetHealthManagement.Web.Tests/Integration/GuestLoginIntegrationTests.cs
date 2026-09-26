using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Integration;

public class GuestLoginIntegrationTests
{
    private const string GuestLoginPath = "/Identity/Account/GuestLogin";

    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 9, 27, 9, 0, 0, TimeSpan.FromHours(9));

    [Fact]
    public async Task HomeAndLogin_ShowGuestLoginForm_WhenEnabled()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);
        using var client = factory.CreateAnonymousClient();

        var homeHtml = await ReadDecodedHtmlAsync(await client.GetAsync("/"));
        var loginHtml = await ReadDecodedHtmlAsync(await client.GetAsync("/Identity/Account/Login?ReturnUrl=%2FPets"));

        foreach (var html in new[] { homeHtml, loginHtml })
        {
            Assert.Contains($"action=\"{GuestLoginPath}\"", html, StringComparison.Ordinal);
            Assert.Contains("ゲストとして試す", html, StringComparison.Ordinal);
            Assert.Contains("登録不要・8時間後に自動削除されます", html, StringComparison.Ordinal);
        }

        Assert.Contains("name=\"returnUrl\" value=\"/Pets\"", loginHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GuestLogin_CreatesGuestWithPrivateSamplePets_AndSignsInForEightHoursWithoutPersistentCookie()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);
        using var client = factory.CreateAnonymousClient();
        var token = await GetAntiforgeryTokenAsync(client, "/Identity/Account/Login");
        var requestedAt = DateTimeOffset.UtcNow;

        using var response = await client.PostAsync(GuestLoginPath, CreateForm(token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/MyPage", response.Headers.Location?.OriginalString);

        var authCookie = GetAuthenticationCookie(factory, response);
        Assert.DoesNotContain("expires=", authCookie, StringComparison.OrdinalIgnoreCase);
        var ticket = ReadAuthenticationTicket(factory, authCookie);
        Assert.True(GuestIdentity.IsGuest(ticket.Principal));
        Assert.False(ticket.Properties.IsPersistent);
        Assert.False(ticket.Properties.AllowRefresh);

        var guest = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var user = await dbContext.Users.SingleAsync();
            var petIds = await dbContext.Pets.Where(x => x.OwnerId == user.Id).Select(x => x.Id).ToListAsync();
            return new
            {
                User = user,
                Claims = await dbContext.UserClaims.Where(x => x.UserId == user.Id).ToListAsync(),
                Pets = await dbContext.Pets.Where(x => x.OwnerId == user.Id).ToListAsync(),
                HealthLogCount = await dbContext.HealthLogs.CountAsync(x => petIds.Contains(x.PetId)),
                ScheduleItemCount = await dbContext.ScheduleItems.CountAsync(x => petIds.Contains(x.PetId)),
                VisitCount = await dbContext.Visits.CountAsync(x => petIds.Contains(x.PetId)),
                ImageAssetCount = await dbContext.ImageAssets.CountAsync()
            };
        });

        Assert.StartsWith(GuestIdentity.UserNamePrefix, guest.User.UserName, StringComparison.Ordinal);
        Assert.Null(guest.User.Email);
        Assert.Null(guest.User.PasswordHash);
        Assert.Equal(GuestIdentity.DisplayName, guest.User.DisplayName);
        Assert.Equal(guest.User.Id, ticket.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);

        var expiryClaim = Assert.Single(guest.Claims);
        Assert.Equal(GuestIdentity.ExpiresAtClaimType, expiryClaim.ClaimType);
        Assert.True(GuestIdentity.TryParseExpiresAt(expiryClaim.ClaimValue, out var expiresAt));
        Assert.InRange(expiresAt, requestedAt + GuestIdentity.Lifetime, DateTimeOffset.UtcNow + GuestIdentity.Lifetime);
        // Cookie の期限は秒単位で保存されるため、claim の期限と秒の範囲で一致すること
        Assert.InRange(ticket.Properties.ExpiresUtc!.Value, expiresAt.AddSeconds(-1), expiresAt);

        Assert.Equal(["こむぎ", "まめ", "ルナ"], guest.Pets.Select(x => x.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.All(guest.Pets, pet => Assert.False(pet.IsPublic));
        Assert.Equal(7, guest.HealthLogCount);
        Assert.Equal(7, guest.ScheduleItemCount);
        Assert.Equal(4, guest.VisitCount);
        Assert.Equal(0, guest.ImageAssetCount);

        using var myPageResponse = await client.GetAsync("/MyPage");
        Assert.Equal(HttpStatusCode.OK, myPageResponse.StatusCode);
        var myPageHtml = await ReadDecodedHtmlAsync(myPageResponse);
        var expectedDeletionTime = expiresAt.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
        Assert.Contains("ゲストとして利用中です。", myPageHtml, StringComparison.Ordinal);
        Assert.Contains($"登録した内容は {expectedDeletionTime} ごろに自動で削除されます。", myPageHtml, StringComparison.Ordinal);
        Assert.Contains("こむぎ", myPageHtml, StringComparison.Ordinal);
        Assert.Contains("ルナ", myPageHtml, StringComparison.Ordinal);
        Assert.Contains("まめ", myPageHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("パスワード変更", myPageHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/Identity/Account/Manage", myPageHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GuestLogin_WithoutAntiforgeryToken_Returns400_AndCreatesNoUser()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);
        using var client = factory.CreateAnonymousClient();

        using var response = await client.PostAsync(GuestLoginPath, CreateForm(token: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await factory.ExecuteDbContextAsync(dbContext => dbContext.Users.CountAsync()));
    }

    [Fact]
    public async Task GuestLogin_WhenDisabled_HidesForms_AndReturns404()
    {
        await using var factory = new IntegrationTestWebApplicationFactory { GuestLoginEnabled = false };
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);
        using var client = factory.CreateAnonymousClient();

        var homeHtml = await ReadDecodedHtmlAsync(await client.GetAsync("/"));
        Assert.DoesNotContain("ゲストとして試す", homeHtml, StringComparison.Ordinal);

        var loginResponse = await client.GetAsync("/Identity/Account/Login");
        var loginHtml = await loginResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ゲストとして試す", WebUtility.HtmlDecode(loginHtml), StringComparison.Ordinal);

        using var response = await client.PostAsync(GuestLoginPath, CreateForm(ExtractAntiforgeryToken(loginHtml)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await factory.ExecuteDbContextAsync(dbContext => dbContext.Users.CountAsync()));
    }

    [Fact]
    public async Task GuestLogin_WhenSameIpExceedsRateLimit_Returns429()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);
        var antiforgery = await factory.CreateAnonymousAntiforgeryRequestDataAsync();

        // 認証 Cookie を保持しないクライアントにして、毎回未ログインとしてゲストログインを送る
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("Cookie", antiforgery.CookieHeaderValue);

        for (var attempt = 0; attempt < GuestLoginRateLimiting.PermitLimit; attempt++)
        {
            using var response = await client.PostAsync(GuestLoginPath, CreateForm(antiforgery.RequestToken));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        using (var limitedResponse = await client.PostAsync(GuestLoginPath, CreateForm(antiforgery.RequestToken)))
        {
            Assert.Equal((HttpStatusCode)429, limitedResponse.StatusCode);
            Assert.True(limitedResponse.Headers.TryGetValues("Retry-After", out _));

            var html = await limitedResponse.Content.ReadAsStringAsync();
            Assert.Contains("少し時間をおいてから、もう一度お試しください。", html, StringComparison.Ordinal);
        }

        Assert.Equal(
            GuestLoginRateLimiting.PermitLimit,
            await factory.ExecuteDbContextAsync(dbContext => dbContext.Users.CountAsync()));
    }

    [Fact]
    public async Task GuestLogin_WhenSignedInAsRegisteredUser_RedirectsWithoutCreatingGuest()
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

        using var response = await client.PostAsync(GuestLoginPath, CreateForm(antiforgery.RequestToken));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/MyPage", response.Headers.Location?.OriginalString);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(0, await CountGuestUsersAsync(factory));
    }

    [Fact]
    public async Task GuestLogin_WhenAlreadySignedInAsGuest_DoesNotCreateAnotherGuest()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);
        using var client = await SignInAsGuestAsync(factory);
        var token = await GetAntiforgeryTokenAsync(client, "/MyPage");

        using var response = await client.PostAsync(GuestLoginPath, CreateForm(token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/MyPage", response.Headers.Location?.OriginalString);
        Assert.Equal(1, await CountGuestUsersAsync(factory));
    }

    [Theory]
    [InlineData("/Pets?page=2", "/Pets?page=2")]
    [InlineData("https://evil.example/", "/MyPage")]
    [InlineData("//evil.example/", "/MyPage")]
    [InlineData("/\\evil.example/", "/MyPage")]
    public async Task GuestLogin_RedirectsOnlyToLocalReturnUrl(string returnUrl, string expectedLocation)
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);
        using var client = factory.CreateAnonymousClient();
        var token = await GetAntiforgeryTokenAsync(client, "/Identity/Account/Login");

        using var response = await client.PostAsync(GuestLoginPath, CreateForm(token, ("returnUrl", returnUrl)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task IdentityManagePages_ReturnForbiddenForGuest_AndStayAvailableToRegisteredUser()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedUsers(dbContext);
            return Task.CompletedTask;
        });
        using var guestClient = await SignInAsGuestAsync(factory);
        using var ownerClient = factory.CreateAuthenticatedClient("owner-user");

        // SetPassword は ManageController ではなく Identity UI 既定の Razor Page が応答する
        foreach (var path in new[]
                 {
                     "/Identity/Account/Manage",
                     "/Identity/Account/Manage/Email",
                     "/Identity/Account/Manage/ChangePassword",
                     "/Identity/Account/Manage/PersonalData",
                     "/Identity/Account/Manage/SetPassword"
                 })
        {
            using var guestResponse = await guestClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, guestResponse.StatusCode);

            using var ownerResponse = await ownerClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        }
    }

    [Fact]
    public async Task GuestCanOpenProfileAndAccountDeletion_ButNotAccountLink()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);
        using var client = await SignInAsGuestAsync(factory);

        using var editProfileResponse = await client.GetAsync("/Account/EditProfile");
        Assert.Equal(HttpStatusCode.OK, editProfileResponse.StatusCode);
        var editProfileHtml = await ReadDecodedHtmlAsync(editProfileResponse);
        Assert.DoesNotContain("href=\"/Identity/Account/Manage\"", editProfileHtml, StringComparison.Ordinal);

        using var deleteResponse = await client.GetAsync("/Account/Delete");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var deleteHtml = await ReadDecodedHtmlAsync(deleteResponse);
        Assert.Contains("<dd>未設定</dd>", deleteHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PetCreateAndEdit_ForGuest_KeepPetPrivateEvenWhenPublicIsPosted()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedUsers(dbContext);
            return Task.CompletedTask;
        });
        using var client = await SignInAsGuestAsync(factory);

        using var createPage = await client.GetAsync("/Pets/Create");
        var createHtml = await createPage.Content.ReadAsStringAsync();
        var decodedCreateHtml = WebUtility.HtmlDecode(createHtml);
        Assert.Contains("ゲストのペットは非公開で保存され、ほかの利用者には表示されません。", decodedCreateHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"IsPublic\"", decodedCreateHtml, StringComparison.Ordinal);

        using (var createResponse = await client.PostAsync(
                   "/Pets/Create",
                   CreateForm(
                       ExtractAntiforgeryToken(createHtml),
                       ("Name", "ゲストの子"),
                       ("SpeciesCode", "DOG"),
                       ("IsPublic", "true"))))
        {
            Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        }

        var createdPet = await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var pet = await dbContext.Pets.SingleAsync(x => x.Name == "ゲストの子");
            // InMemory は RowVersion を採番しないため、編集の同時更新チェック用に値を入れる
            pet.RowVersion ??= [1, 0, 0, 0];
            await dbContext.SaveChangesAsync();
            return pet;
        });
        Assert.False(createdPet.IsPublic);

        var editToken = await GetAntiforgeryTokenAsync(client, $"/Pets/Edit/{createdPet.Id}");
        using (var editResponse = await client.PostAsync(
                   $"/Pets/Edit/{createdPet.Id}",
                   CreateForm(
                       editToken,
                       ("Name", "ゲストの子"),
                       ("SpeciesCode", "DOG"),
                       ("IsPublic", "true"),
                       ("RowVersion", RowVersionCodec.Encode(createdPet.RowVersion)!))))
        {
            Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
        }

        Assert.False(await factory.ExecuteDbContextAsync(dbContext =>
            dbContext.Pets.Where(x => x.Id == createdPet.Id).Select(x => x.IsPublic).SingleAsync()));

        using var otherClient = factory.CreateAuthenticatedClient("other-user");
        var otherPetsHtml = await ReadDecodedHtmlAsync(await otherClient.GetAsync("/Pets"));
        Assert.DoesNotContain("ゲストの子", otherPetsHtml, StringComparison.Ordinal);
        using var otherDetailsResponse = await otherClient.GetAsync($"/Pets/Details/{createdPet.Id}");
        Assert.Equal(HttpStatusCode.NotFound, otherDetailsResponse.StatusCode);
    }

    private static async Task<HttpClient> SignInAsGuestAsync(IntegrationTestWebApplicationFactory factory)
    {
        var client = factory.CreateAnonymousClient();
        var token = await GetAntiforgeryTokenAsync(client, "/Identity/Account/Login");

        using var response = await client.PostAsync(GuestLoginPath, CreateForm(token));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return client;
    }

    private static Task<int> CountGuestUsersAsync(IntegrationTestWebApplicationFactory factory)
    {
        return factory.ExecuteDbContextAsync(dbContext =>
            dbContext.UserClaims.CountAsync(x => x.ClaimType == GuestIdentity.ExpiresAtClaimType));
    }

    private static string GetAuthenticationCookie(IntegrationTestWebApplicationFactory factory, HttpResponseMessage response)
    {
        var cookieName = GetCookieOptions(factory).Cookie.Name!;
        return response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(cookieName + "=", StringComparison.Ordinal));
    }

    private static AuthenticationTicket ReadAuthenticationTicket(IntegrationTestWebApplicationFactory factory, string setCookieHeader)
    {
        var options = GetCookieOptions(factory);
        var protectedTicket = setCookieHeader.Split(';', 2)[0][(options.Cookie.Name!.Length + 1)..];
        var ticket = options.TicketDataFormat.Unprotect(protectedTicket);

        Assert.NotNull(ticket);
        return ticket;
    }

    private static CookieAuthenticationOptions GetCookieOptions(IntegrationTestWebApplicationFactory factory)
    {
        return factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return ExtractAntiforgeryToken(await response.Content.ReadAsStringAsync());
    }


    private static FormUrlEncodedContent CreateForm(string? token, params (string Name, string Value)[] fields)
    {
        var values = fields
            .Select(x => new KeyValuePair<string, string>(x.Name, x.Value))
            .ToList();

        if (token is not null)
        {
            values.Add(new KeyValuePair<string, string>("__RequestVerificationToken", token));
        }

        return new FormUrlEncodedContent(values);
    }

    private static void SeedUsers(ApplicationDbContext dbContext)
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

        dbContext.Pets.Add(new Pet
        {
            Id = 100,
            OwnerId = "other-user",
            Name = "Public Cat",
            SpeciesCode = "CAT",
            IsPublic = true,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
        });
    }

    private static async Task<string> ReadDecodedHtmlAsync(HttpResponseMessage response)
    {
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
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

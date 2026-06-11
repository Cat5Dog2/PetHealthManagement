using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.E2ETests.Infrastructure;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.E2ETests;

[Trait("Category", "E2E")]
public sealed class HomePageE2ETests(E2EWebApplicationFactory factory)
    : PageTest, IClassFixture<E2EWebApplicationFactory>
{
    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 3, 30, 9, 0, 0, TimeSpan.FromHours(9));

    [E2EFact]
    public async Task AnonymousUser_CanOpenHome_AndProtectedPageRedirectsToLogin()
    {
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);

        await Page.GotoAsync(await AbsoluteUrlAsync("/"));

        await Expect(Page).ToHaveTitleAsync(new Regex("^トップ - うちの子健康カルテ$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "うちの子健康カルテ" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "ログイン" }).First).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "新規登録" }).First).ToBeVisibleAsync();

        await Page.GotoAsync(await AbsoluteUrlAsync("/MyPage"));

        await Expect(Page).ToHaveURLAsync(new Regex(@"/Identity/Account/Login\?ReturnUrl=%2FMyPage$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "ログイン" })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("メールアドレス")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("パスワード", new() { Exact = true })).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task AuthenticatedUser_CanSeeMyPageAndSearchPets()
    {
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedUsers(dbContext);
            SeedPets(dbContext);
            return Task.CompletedTask;
        });

        await Page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            [TestAuthenticationDefaults.UserIdHeaderName] = "owner-user",
            [TestAuthenticationDefaults.UserNameHeaderName] = "owner@example.com"
        });

        await Page.GotoAsync(await AbsoluteUrlAsync("/MyPage"));

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Owner Display" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("owner@example.com")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Mugi" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Other Private Pet")).ToHaveCountAsync(0);

        await Page.GetByRole(AriaRole.Link, new() { Name = "ペット", Exact = true }).First.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/Pets$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "ペット" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Mugi" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Public Cat" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Other Private Pet")).ToHaveCountAsync(0);

        await Page.GetByLabel("名前").FillAsync("Mugi");
        await Page.GetByRole(AriaRole.Button, new() { Name = "検索" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"[?&]nameKeyword=Mugi"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Mugi" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Public Cat")).ToHaveCountAsync(0);
    }

    private async Task<string> AbsoluteUrlAsync(string path)
    {
        return new Uri(await factory.GetServerAddressAsync(), path).ToString();
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
    }

    private static void SeedPets(ApplicationDbContext dbContext)
    {
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
                Name = "Public Cat",
                SpeciesCode = "CAT",
                IsPublic = true,
                CreatedAt = SeedTimestamp.AddMinutes(-1),
                UpdatedAt = SeedTimestamp.AddMinutes(-1)
            },
            new Pet
            {
                Id = 3,
                OwnerId = "other-user",
                Name = "Other Private Pet",
                SpeciesCode = "CAT",
                IsPublic = false,
                CreatedAt = SeedTimestamp.AddMinutes(-2),
                UpdatedAt = SeedTimestamp.AddMinutes(-2)
            });
    }
}

using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using PetHealthManagement.Web.E2ETests.Infrastructure;

namespace PetHealthManagement.Web.E2ETests;

[Trait("Category", "E2E")]
public sealed class GuestLoginE2ETests(E2EWebApplicationFactory factory)
    : PageTest, IClassFixture<E2EWebApplicationFactory>
{
    [E2EFact]
    public async Task GuestLoginFromLoginPage_ShowsSamplePetsAndBannerOnMyPage()
    {
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);

        await Page.GotoAsync(await AbsoluteUrlAsync("/Identity/Account/Login"));
        await Expect(Page.GetByText("登録不要・8時間後に自動削除されます")).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "ゲストとして試す" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/MyPage$"));
        await ExpectGuestMyPageAsync();

        // 再読み込みしてもゲストのまま（認証 Cookie が保持されている）
        await Page.ReloadAsync();
        await ExpectGuestMyPageAsync();
    }

    [E2EFact]
    public async Task GuestLoginFromHome_OpensMyPageAsGuest()
    {
        await factory.ResetDatabaseAsync(_ => Task.CompletedTask);

        await Page.GotoAsync(await AbsoluteUrlAsync("/"));
        await Page.GetByRole(AriaRole.Button, new() { Name = "ゲストとして試す" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/MyPage$"));
        await ExpectGuestMyPageAsync();
    }

    private async Task ExpectGuestMyPageAsync()
    {
        await Expect(Page.GetByText("ゲストとして利用中です。")).ToBeVisibleAsync();
        await Expect(Page.GetByText(new Regex(@"登録した内容は \d{4}/\d{2}/\d{2} \d{2}:\d{2} ごろに自動で削除されます。"))).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "ゲスト", Exact = true })).ToBeVisibleAsync();

        foreach (var petName in new[] { "こむぎ", "ルナ", "まめ" })
        {
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = petName, Exact = true })).ToBeVisibleAsync();
        }

        // サンプルのペットはすべて非公開
        await Expect(Page.Locator(".pet-card .chip-muted")).ToHaveCountAsync(3);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "パスワード変更" })).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "アカウント", Exact = true })).ToHaveCountAsync(0);
    }

    private async Task<string> AbsoluteUrlAsync(string path)
    {
        return new Uri(await factory.GetServerAddressAsync(), path).ToString();
    }
}

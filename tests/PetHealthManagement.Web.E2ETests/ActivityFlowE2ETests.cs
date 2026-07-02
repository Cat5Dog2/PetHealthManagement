using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.E2ETests.Infrastructure;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.E2ETests;

[Trait("Category", "E2E")]
public sealed class ActivityFlowE2ETests(E2EWebApplicationFactory factory)
    : PageTest, IClassFixture<E2EWebApplicationFactory>
{
    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 3, 30, 9, 0, 0, TimeSpan.FromHours(9));

    [E2EFact]
    public async Task AuthenticatedUser_CanCreatePetWithoutPhoto()
    {
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedUsers(dbContext);
            return Task.CompletedTask;
        });
        await AuthenticateOwnerAsync();

        await Page.GotoAsync(await AbsoluteUrlAsync("/MyPage"));
        await Page.GetByRole(AriaRole.Link, new() { Name = "ペット登録" }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "ペット登録" })).ToBeVisibleAsync();

        await Page.GetByLabel("名前").FillAsync("Sora");
        await Page.GetByLabel("種別").SelectOptionAsync("DOG");
        await Page.GetByLabel("品種").FillAsync("柴犬");
        await Page.GetByLabel("性別").FillAsync("メス");
        await Page.GetByLabel("誕生日").FillAsync("2024-02-03");
        await Page.GetByLabel("迎えた日").FillAsync("2024-04-05");
        await Page.GetByLabel("公開する").UncheckAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "保存" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/MyPage$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sora" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("非公開", new() { Exact = true })).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task AuthenticatedUser_CanCreateNonImagePetActivities()
    {
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedUsers(dbContext);
            SeedOwnedPet(dbContext);
            return Task.CompletedTask;
        });
        await AuthenticateOwnerAsync();

        await CreateHealthLogAsync();
        await CreateScheduleItemAndMarkDoneAsync();
        await CreateVisitAsync();
    }

    private async Task CreateHealthLogAsync()
    {
        await Page.GotoAsync(await AbsoluteUrlAsync("/HealthLogs?petId=1"));
        await Page.GetByRole(AriaRole.Link, new() { Name = "記録する" }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "健康ログ作成" })).ToBeVisibleAsync();

        await Page.GetByLabel("記録日時").FillAsync("2026-04-01T08:30");
        await Page.GetByLabel("体重(kg)").FillAsync("5.4");
        await Page.GetByLabel("食事量(g)").FillAsync("120");
        await Page.GetByLabel("散歩時間(分)").FillAsync("35");
        await Page.GetByLabel("便の様子").SelectOptionAsync("良好");
        await Page.GetByLabel("メモ").FillAsync("朝から元気");
        await Page.GetByRole(AriaRole.Button, new() { Name = "保存" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/HealthLogs\?petId=1$"));
        await Expect(Page.GetByText("2026/04/01 08:30")).ToBeVisibleAsync();
        await Expect(Page.GetByText("5.4 kg")).ToBeVisibleAsync();
        await Expect(Page.GetByText("120 g")).ToBeVisibleAsync();
        await Expect(Page.GetByText("朝から元気")).ToBeVisibleAsync();
    }

    private async Task CreateScheduleItemAndMarkDoneAsync()
    {
        await Page.GotoAsync(await AbsoluteUrlAsync("/ScheduleItems?petId=1"));
        await Page.GetByRole(AriaRole.Link, new() { Name = "予定を追加" }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "予定作成" })).ToBeVisibleAsync();

        await Page.GetByLabel("期日").FillAsync("2026-04-10");
        await Page.GetByLabel("種別").SelectOptionAsync(ScheduleItemTypeCatalog.Vaccine);
        await Page.GetByLabel("タイトル").FillAsync("狂犬病ワクチン");
        await Page.GetByLabel("メモ").FillAsync("午前中に受診");
        await Page.GetByRole(AriaRole.Button, new() { Name = "保存" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/ScheduleItems\?petId=1$"));
        var scheduleCards = Page.GetByRole(AriaRole.Article);
        await Expect(scheduleCards.GetByText("2026/04/10")).ToBeVisibleAsync();
        await Expect(scheduleCards.GetByText("ワクチン", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(scheduleCards.GetByText("狂犬病ワクチン")).ToBeVisibleAsync();
        await Expect(scheduleCards.GetByText("未完了", new() { Exact = true })).ToBeVisibleAsync();

        await Page.Locator("input[type=checkbox][name=isDone][value=true]").CheckAsync();

        await Expect(scheduleCards.GetByText("完了", new() { Exact = true })).ToBeVisibleAsync();
    }

    private async Task CreateVisitAsync()
    {
        await Page.GotoAsync(await AbsoluteUrlAsync("/Visits?petId=1"));
        await Page.GetByRole(AriaRole.Link, new() { Name = "通院を記録" }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "通院作成" })).ToBeVisibleAsync();

        await Page.GetByLabel("通院日").FillAsync("2026-04-15");
        await Page.GetByLabel("病院名").FillAsync("中央どうぶつ病院");
        await Page.GetByLabel("診断").FillAsync("軽い皮膚炎");
        await Page.GetByLabel("処方").FillAsync("塗り薬");
        await Page.GetByLabel("メモ").FillAsync("一週間後に再診");
        await Page.GetByRole(AriaRole.Button, new() { Name = "保存" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/Visits\?petId=1$"));
        await Expect(Page.GetByText("2026/04/15")).ToBeVisibleAsync();
        await Expect(Page.GetByText("中央どうぶつ病院")).ToBeVisibleAsync();
        await Expect(Page.GetByText("軽い皮膚炎")).ToBeVisibleAsync();
        await Expect(Page.GetByText("塗り薬")).ToBeVisibleAsync();
    }

    private async Task AuthenticateOwnerAsync()
    {
        await Page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            [TestAuthenticationDefaults.UserIdHeaderName] = "owner-user",
            [TestAuthenticationDefaults.UserNameHeaderName] = "owner@example.com"
        });
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

    private static void SeedOwnedPet(ApplicationDbContext dbContext)
    {
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
    }
}

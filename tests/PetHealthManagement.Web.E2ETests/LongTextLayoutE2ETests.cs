using System.Text.Json;
using Microsoft.Playwright.Xunit;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.E2ETests.Infrastructure;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.E2ETests;

[Trait("Category", "E2E")]
public sealed class LongTextLayoutE2ETests(E2EWebApplicationFactory factory)
    : PageTest, IClassFixture<E2EWebApplicationFactory>
{
    private const int LongestPetId = 1;

    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 3, 30, 9, 0, 0, TimeSpan.FromHours(9));

    // 空白を含まない最大長の入力。日本語と違い行分割の機会がないため、
    // 折り返し指定が無いと枠外にはみ出す。
    private static readonly string LongestName =
        new('C', InputValidationLimits.Pets.NameMaxLength);

    private static readonly string LongestBreed =
        new('A', InputValidationLimits.Pets.BreedMaxLength);

    private static readonly string LongestDisplayName =
        new('B', InputValidationLimits.Profile.DisplayNameMaxLength);

    // flex/grid の子は min-width: auto のため、折り返せない文字列があると
    // 「自身の中で溢れる(scrollWidth)」のではなく「箱ごと親からはみ出す」。
    // どちらの壊れ方も拾えるよう両方を数える。
    private const string OverflowProbeScript = """
        (args) => {
          const root = document.documentElement;
          let matched = 0;
          let spillingOutOfContainer = 0;
          let internalOverflow = 0;

          for (const container of document.querySelectorAll(args.container)) {
            const containerRight = Math.round(container.getBoundingClientRect().right);
            for (const el of container.querySelectorAll(args.child)) {
              matched++;
              if (Math.round(el.getBoundingClientRect().right) > containerRight) spillingOutOfContainer++;
              if (el.scrollWidth > el.clientWidth + 1) internalOverflow++;
            }
          }

          return {
            matched,
            spillingOutOfContainer,
            internalOverflow,
            horizontalScroll: root.scrollWidth - root.clientWidth
          };
        }
        """;

    private const string PetListProbeScript = """
        () => {
          const navbar = document.querySelector('.app-topbar .navbar');
          const main = document.querySelector('.app-main');
          const list = document.querySelector('.pet-list');
          const inner = (el) => el.getBoundingClientRect().left + parseFloat(getComputedStyle(el).paddingLeft);

          return {
            columns: getComputedStyle(list).gridTemplateColumns.split(' ').length,
            cardWidth: document.querySelector('.pet-card').getBoundingClientRect().width,
            headerBodyOffset: Math.abs(inner(navbar) - inner(main))
          };
        }
        """;

    [E2EFact]
    public async Task PetList_KeepsLayoutIntact_AcrossViewportsAndLongestInput()
    {
        await SeedAndSignInAsync();

        // 390: スマホ / 768: 1列→2列の境界 / 880-980: 2列→3列の切替をまたぐ / 1280, 1920: PC
        int[] viewportWidths = [390, 768, 880, 900, 920, 940, 960, 980, 1280, 1920];
        var columnsByWidth = new Dictionary<int, int>();

        foreach (var width in viewportWidths)
        {
            await Page.SetViewportSizeAsync(width, 900);
            await GotoAsync("/Pets");

            AssertNoOverflow($"{width}px /Pets", await ProbeOverflowAsync(".pet-card", "h1, h2, h3, dd"));

            var probe = await Page.EvaluateAsync<JsonElement>(PetListProbeScript);
            var columns = probe.GetProperty("columns").GetInt32();
            var cardWidth = probe.GetProperty("cardWidth").GetDouble();
            var headerBodyOffset = probe.GetProperty("headerBodyOffset").GetDouble();

            columnsByWidth[width] = columns;

            Assert.True(
                headerBodyOffset <= 1,
                $"{width}px: ヘッダーと本文の左端が {headerBodyOffset}px ずれている");

            // 3列にするのはカードが十分な幅を保てるときだけ、という意図を固定する
            if (columns >= 3)
            {
                Assert.True(
                    cardWidth >= 279,
                    $"{width}px: {columns}列でカード幅が {cardWidth}px しかない");
            }
        }

        Assert.Equal(1, columnsByWidth[390]);
        Assert.Equal(2, columnsByWidth[768]);
        Assert.Equal(3, columnsByWidth[1280]);
        Assert.Equal(3, columnsByWidth[1920]);

        // 幅を広げたときに列が減らないこと（切替前後の逆転を防ぐ）
        var ordered = viewportWidths.Select(width => columnsByWidth[width]).ToArray();
        for (var i = 1; i < ordered.Length; i++)
        {
            Assert.True(
                ordered[i] >= ordered[i - 1],
                $"{viewportWidths[i]}px で列数が {ordered[i - 1]} から {ordered[i]} に減っている");
        }
    }

    [E2EFact]
    public async Task PetDetails_KeepsLongestNameInsideCard()
    {
        await SeedAndSignInAsync();

        foreach (var width in (int[])[390, 1280])
        {
            await Page.SetViewportSizeAsync(width, 900);
            await GotoAsync($"/Pets/Details/{LongestPetId}");

            // 詳細のペット名は h1。一覧の h2/h3 とは別セレクタなので個別に押さえる。
            AssertNoOverflow(
                $"{width}px /Pets/Details",
                await ProbeOverflowAsync(".surface", ".pet-card__title h1"));
        }
    }

    [E2EFact]
    public async Task RecordPetPicker_KeepsLongestNameInsideCard()
    {
        await SeedAndSignInAsync();

        await Page.SetViewportSizeAsync(390, 900);
        await GotoAsync("/HealthLogs/Record");

        AssertNoOverflow(
            "390px /HealthLogs/Record",
            await ProbeOverflowAsync(".record-pick__item", ".record-pick__name"));
    }

    private async Task<JsonElement> ProbeOverflowAsync(string container, string child)
    {
        return await Page.EvaluateAsync<JsonElement>(
            OverflowProbeScript,
            new { container, child });
    }

    private static void AssertNoOverflow(string context, JsonElement probe)
    {
        var matched = probe.GetProperty("matched").GetInt32();
        var spilling = probe.GetProperty("spillingOutOfContainer").GetInt32();
        var internalOverflow = probe.GetProperty("internalOverflow").GetInt32();
        var horizontalScroll = probe.GetProperty("horizontalScroll").GetDouble();

        // 検査対象が0件だと以降の assert が素通りするため、まず件数を確かめる
        Assert.True(matched > 0, $"{context}: 検査対象の要素が1件も見つからない");
        Assert.True(
            spilling == 0,
            $"{context}: 親要素の右端からはみ出した要素が {spilling} 件ある");
        Assert.True(
            internalOverflow == 0,
            $"{context}: 要素内で溢れているテキストが {internalOverflow} 件ある");
        Assert.True(
            horizontalScroll <= 0,
            $"{context}: ページに横スクロールが発生している ({horizontalScroll}px)");
    }

    private async Task GotoAsync(string path)
    {
        await Page.GotoAsync(new Uri(await factory.GetServerAddressAsync(), path).ToString());
    }

    private async Task SeedAndSignInAsync()
    {
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwner(dbContext);
            SeedPets(dbContext);
            return Task.CompletedTask;
        });

        await Page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            [TestAuthenticationDefaults.UserIdHeaderName] = "owner-user",
            [TestAuthenticationDefaults.UserNameHeaderName] = "owner@example.com"
        });
    }

    private static void SeedOwner(ApplicationDbContext dbContext)
    {
        dbContext.Users.Add(new ApplicationUser
        {
            Id = "owner-user",
            UserName = "owner-user",
            DisplayName = LongestDisplayName,
            Email = "owner@example.com"
        });
    }

    private static void SeedPets(ApplicationDbContext dbContext)
    {
        dbContext.Pets.AddRange(
            new Pet
            {
                Id = LongestPetId,
                OwnerId = "owner-user",
                Name = LongestName,
                SpeciesCode = "DOG",
                Breed = LongestBreed,
                IsPublic = true,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp
            },
            new Pet
            {
                Id = 2,
                OwnerId = "owner-user",
                Name = "こむぎ",
                SpeciesCode = "DOG",
                Breed = "柴犬",
                IsPublic = true,
                CreatedAt = SeedTimestamp.AddMinutes(-1),
                UpdatedAt = SeedTimestamp.AddMinutes(-1)
            },
            new Pet
            {
                Id = 3,
                OwnerId = "owner-user",
                Name = "そら",
                SpeciesCode = "CAT",
                Breed = "スコティッシュフォールド",
                IsPublic = true,
                CreatedAt = SeedTimestamp.AddMinutes(-2),
                UpdatedAt = SeedTimestamp.AddMinutes(-2)
            },
            new Pet
            {
                Id = 4,
                OwnerId = "owner-user",
                Name = "まめ",
                SpeciesCode = "RABBIT",
                Breed = "ネザーランドドワーフ",
                IsPublic = false,
                CreatedAt = SeedTimestamp.AddMinutes(-3),
                UpdatedAt = SeedTimestamp.AddMinutes(-3)
            });
    }
}

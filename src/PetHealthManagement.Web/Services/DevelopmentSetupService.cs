using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public class DevelopmentSetupService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IHostEnvironment hostEnvironment,
    IOptions<DevelopmentSetupOptions> options,
    ILogger<DevelopmentSetupService> logger) : IDevelopmentSetupService
{
    public const string AdminRoleName = "Admin";
    private static readonly TimeSpan JapanStandardTimeOffset = TimeSpan.FromHours(9);

    public Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying EF Core migrations for environment {EnvironmentName}.", hostEnvironment.EnvironmentName);
        return dbContext.Database.MigrateAsync(cancellationToken);
    }

    public async Task SeedDevelopmentIdentityAsync(CancellationToken cancellationToken = default)
    {
        EnsureDevelopmentEnvironment("Development identity seeding");
        await EnsureAdminUserAsync(cancellationToken);

        logger.LogInformation(
            "Development identity seed completed. role={RoleName}",
            AdminRoleName);
    }

    public async Task SeedAdminIdentityAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAdminUserAsync(cancellationToken);

        logger.LogInformation(
            "Admin identity seed completed. environment={EnvironmentName} role={RoleName}",
            hostEnvironment.EnvironmentName,
            AdminRoleName);
    }

    public async Task SeedDevelopmentDemoDataAsync(CancellationToken cancellationToken = default)
    {
        EnsureDevelopmentEnvironment("Development demo data seeding");
        await SeedDemoDataAsync(cancellationToken);
    }

    public async Task SeedDemoDataAsync(CancellationToken cancellationToken = default)
    {
        var adminUser = await EnsureAdminUserAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow.ToOffset(JapanStandardTimeOffset);
        var today = DateOnly.FromDateTime(now.Date);
        var createdPetCount = 0;
        var createdDemoUserCount = 0;

        createdPetCount += await SeedDemoPetsAsync(
            adminUser.Id,
            BuildAdminDemoPetDefinitions(today),
            now,
            cancellationToken);

        var demoUserPassword = ResolveDemoUserPassword();
        foreach (var definition in BuildDemoUserDefinitions(today))
        {
            var result = await EnsureDemoUserAsync(definition, demoUserPassword, cancellationToken);
            if (result.Created)
            {
                createdDemoUserCount++;
            }

            createdPetCount += await SeedDemoPetsAsync(
                result.User.Id,
                definition.Pets,
                now,
                cancellationToken);
        }

        if (createdPetCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Demo data seed completed. environment={EnvironmentName} createdDemoUserCount={CreatedDemoUserCount} createdPetCount={CreatedPetCount}",
            hostEnvironment.EnvironmentName,
            createdDemoUserCount,
            createdPetCount);
    }

    private async Task<ApplicationUser> EnsureAdminUserAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await roleManager.RoleExistsAsync(AdminRoleName))
        {
            var createRoleResult = await roleManager.CreateAsync(new IdentityRole(AdminRoleName));
            EnsureIdentitySucceeded(createRoleResult, "Failed to create the Admin role.");
            logger.LogInformation("Created role {RoleName}.", AdminRoleName);
        }

        var adminEmail = options.Value.AdminEmail.Trim();
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            throw new InvalidOperationException(
                "DevelopmentSetup:AdminEmail is required. Configure it in .env, user-secrets, or via the DevelopmentSetup__AdminEmail environment variable.");
        }

        var adminDisplayName = string.IsNullOrWhiteSpace(options.Value.AdminDisplayName)
            ? "Development Admin"
            : options.Value.AdminDisplayName.Trim();

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            var adminPassword = options.Value.AdminPassword;
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                throw new InvalidOperationException(
                    "DevelopmentSetup:AdminPassword is required to create the admin user. Configure it in .env, user-secrets, or via the DevelopmentSetup__AdminPassword environment variable.");
            }

            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = adminDisplayName
            };

            var createUserResult = await userManager.CreateAsync(adminUser, adminPassword);
            EnsureIdentitySucceeded(createUserResult, "Failed to create the admin user.");
            logger.LogInformation("Created admin user for the configured Admin email.");
        }
        else
        {
            var requiresUpdate = false;

            if (!string.Equals(adminUser.UserName, adminEmail, StringComparison.OrdinalIgnoreCase))
            {
                adminUser.UserName = adminEmail;
                requiresUpdate = true;
            }

            if (!string.Equals(adminUser.Email, adminEmail, StringComparison.OrdinalIgnoreCase))
            {
                adminUser.Email = adminEmail;
                requiresUpdate = true;
            }

            if (!adminUser.EmailConfirmed)
            {
                adminUser.EmailConfirmed = true;
                requiresUpdate = true;
            }

            if (string.IsNullOrWhiteSpace(adminUser.DisplayName))
            {
                adminUser.DisplayName = adminDisplayName;
                requiresUpdate = true;
            }

            if (requiresUpdate)
            {
                var updateUserResult = await userManager.UpdateAsync(adminUser);
                EnsureIdentitySucceeded(updateUserResult, "Failed to update the admin user.");
            }

            if (!await userManager.HasPasswordAsync(adminUser))
            {
                var adminPassword = options.Value.AdminPassword;
                if (string.IsNullOrWhiteSpace(adminPassword))
                {
                    throw new InvalidOperationException(
                        "DevelopmentSetup:AdminPassword is required because the existing admin user does not have a password.");
                }

                var addPasswordResult = await userManager.AddPasswordAsync(adminUser, adminPassword);
                EnsureIdentitySucceeded(addPasswordResult, "Failed to set the admin password.");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, AdminRoleName))
        {
            var addToRoleResult = await userManager.AddToRoleAsync(adminUser, AdminRoleName);
            EnsureIdentitySucceeded(addToRoleResult, "Failed to add the admin user to the Admin role.");
        }

        return adminUser;
    }

    private async Task<(ApplicationUser User, bool Created)> EnsureDemoUserAsync(
        DemoUserDefinition definition,
        string demoUserPassword,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var demoUser = await userManager.FindByEmailAsync(definition.Email);
        if (demoUser is null)
        {
            demoUser = new ApplicationUser
            {
                UserName = definition.Email,
                Email = definition.Email,
                EmailConfirmed = true,
                DisplayName = definition.DisplayName
            };

            var createUserResult = await userManager.CreateAsync(demoUser, demoUserPassword);
            EnsureIdentitySucceeded(createUserResult, "Failed to create the demo user.");
            logger.LogInformation("Created demo user for a configured demo email.");

            return (demoUser, true);
        }

        var requiresUpdate = false;
        if (!string.Equals(demoUser.UserName, definition.Email, StringComparison.OrdinalIgnoreCase))
        {
            demoUser.UserName = definition.Email;
            requiresUpdate = true;
        }

        if (!string.Equals(demoUser.Email, definition.Email, StringComparison.OrdinalIgnoreCase))
        {
            demoUser.Email = definition.Email;
            requiresUpdate = true;
        }

        if (!demoUser.EmailConfirmed)
        {
            demoUser.EmailConfirmed = true;
            requiresUpdate = true;
        }

        if (string.IsNullOrWhiteSpace(demoUser.DisplayName))
        {
            demoUser.DisplayName = definition.DisplayName;
            requiresUpdate = true;
        }

        if (requiresUpdate)
        {
            var updateUserResult = await userManager.UpdateAsync(demoUser);
            EnsureIdentitySucceeded(updateUserResult, "Failed to update the demo user.");
        }

        if (!await userManager.HasPasswordAsync(demoUser))
        {
            var addPasswordResult = await userManager.AddPasswordAsync(demoUser, demoUserPassword);
            EnsureIdentitySucceeded(addPasswordResult, "Failed to set the demo user password.");
        }

        return (demoUser, false);
    }

    private async Task<int> SeedDemoPetsAsync(
        string ownerId,
        IReadOnlyList<DemoPetDefinition> definitions,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var createdPetCount = 0;
        foreach (var definition in definitions)
        {
            var exists = await dbContext.Pets
                .AnyAsync(x => x.OwnerId == ownerId && x.Name == definition.Name, cancellationToken);
            if (exists)
            {
                continue;
            }

            dbContext.Pets.Add(definition.CreatePet(ownerId, now));
            createdPetCount++;
        }

        return createdPetCount;
    }

    private string ResolveDemoUserPassword()
    {
        var demoUserPassword = options.Value.DemoUserPassword;
        if (!string.IsNullOrWhiteSpace(demoUserPassword))
        {
            return demoUserPassword;
        }

        if (hostEnvironment.IsDevelopment() && !string.IsNullOrWhiteSpace(options.Value.AdminPassword))
        {
            return options.Value.AdminPassword;
        }

        throw new InvalidOperationException(
            "DevelopmentSetup:DemoUserPassword is required to create demo users. Configure it in .env, user-secrets, or via the DevelopmentSetup__DemoUserPassword environment variable.");
    }

    private void EnsureDevelopmentEnvironment(string operationName)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            throw new InvalidOperationException($"{operationName} can only run in the Development environment.");
        }
    }

    private static IReadOnlyList<DemoPetDefinition> BuildAdminDemoPetDefinitions(DateOnly today)
    {
        return
        [
            new DemoPetDefinition(
                Name: "こむぎ",
                SpeciesCode: "DOG",
                Breed: "トイプードル",
                Sex: "メス",
                BirthDate: new DateOnly(2021, 4, 12),
                AdoptedDate: new DateOnly(2021, 7, 3),
                IsPublic: true,
                HealthLogs:
                [
                    new DemoHealthLogDefinition(today.AddDays(-1), 20, 30, 4.8, 85, 45, "普通", "夕方の散歩もよく歩いた。"),
                    new DemoHealthLogDefinition(today.AddDays(-4), 7, 40, null, 80, 25, "少し柔らかい", "朝ごはんは半分残した。"),
                    new DemoHealthLogDefinition(today.AddDays(-9), 21, 10, 4.7, 90, 50, "良好", "フィラリア薬を服用。")
                ],
                ScheduleItems:
                [
                    new DemoScheduleItemDefinition(today.AddDays(4), ScheduleItemTypeCatalog.Medicine, "フィラリア予防薬", "夕食後に投薬予定。", false),
                    new DemoScheduleItemDefinition(today.AddDays(18), ScheduleItemTypeCatalog.Vaccine, "混合ワクチン接種", "午前中に青葉どうぶつ病院へ行く。", false),
                    new DemoScheduleItemDefinition(today.AddDays(-10), ScheduleItemTypeCatalog.Other, "トリミング予約", "足先と顔まわりを短めに整えた。", true)
                ],
                Visits:
                [
                    new DemoVisitDefinition(today.AddDays(-32), "青葉どうぶつ病院", "外耳炎の経過確認", "点耳薬を1日2回", "赤みは改善傾向。"),
                    new DemoVisitDefinition(today.AddDays(-180), "青葉どうぶつ病院", "年次健康診断", "なし", "血液検査は異常なし。")
                ]),
            new DemoPetDefinition(
                Name: "ルナ",
                SpeciesCode: "CAT",
                Breed: "ミックス",
                Sex: "メス",
                BirthDate: new DateOnly(2019, 9, 20),
                AdoptedDate: new DateOnly(2020, 1, 11),
                IsPublic: true,
                HealthLogs:
                [
                    new DemoHealthLogDefinition(today.AddDays(-2), 22, 0, 3.9, 60, null, "良好", "水をよく飲んでいる。"),
                    new DemoHealthLogDefinition(today.AddDays(-7), 8, 15, 3.8, 55, null, "普通", "毛玉ケアフードへ切り替え中。")
                ],
                ScheduleItems:
                [
                    new DemoScheduleItemDefinition(today.AddDays(1), ScheduleItemTypeCatalog.Medicine, "ノミ・ダニ予防薬", "首元に滴下する。", false),
                    new DemoScheduleItemDefinition(today.AddDays(30), ScheduleItemTypeCatalog.Visit, "腎臓チェック再診", "尿検査の結果を持参。", false)
                ],
                Visits:
                [
                    new DemoVisitDefinition(today.AddDays(-45), "みなと猫クリニック", "軽い歯肉炎", "デンタルジェル", "歯みがき頻度を増やす。")
                ]),
            new DemoPetDefinition(
                Name: "まめ",
                SpeciesCode: "RABBIT",
                Breed: "ネザーランドドワーフ",
                Sex: "オス",
                BirthDate: new DateOnly(2023, 2, 5),
                AdoptedDate: new DateOnly(2023, 4, 1),
                IsPublic: false,
                HealthLogs:
                [
                    new DemoHealthLogDefinition(today.AddDays(-1), 19, 30, 1.4, 35, null, "良好", "牧草の食いつき良好。"),
                    new DemoHealthLogDefinition(today.AddDays(-5), 20, 0, 1.4, 32, null, "普通", "部屋んぽは短め。")
                ],
                ScheduleItems:
                [
                    new DemoScheduleItemDefinition(today.AddDays(14), ScheduleItemTypeCatalog.Visit, "爪切りと健康チェック", "小動物ケアクリニックに予約済み。", false),
                    new DemoScheduleItemDefinition(today.AddDays(-20), ScheduleItemTypeCatalog.Other, "ケージ丸洗い", "床材と給水ボトルも交換。", true)
                ],
                Visits:
                [
                    new DemoVisitDefinition(today.AddDays(-70), "小動物ケアクリニック", "爪切り", null, "体重は安定。")
                ])
        ];
    }

    private static IReadOnlyList<DemoUserDefinition> BuildDemoUserDefinitions(DateOnly today)
    {
        return
        [
            new DemoUserDefinition(
                Email: "demo.sato@example.com",
                DisplayName: "佐藤 花",
                Pets:
                [
                    new DemoPetDefinition(
                        Name: "そら",
                        SpeciesCode: "CAT",
                        Breed: "スコティッシュフォールド",
                        Sex: "オス",
                        BirthDate: new DateOnly(2020, 5, 18),
                        AdoptedDate: new DateOnly(2020, 8, 2),
                        IsPublic: true,
                        HealthLogs:
                        [
                            new DemoHealthLogDefinition(today.AddDays(-1), 7, 20, 4.6, 58, null, "良好", "朝からよく遊んだ。"),
                            new DemoHealthLogDefinition(today.AddDays(-6), 21, 0, 4.6, 62, null, "普通", "ウェットフードを少し追加。")
                        ],
                        ScheduleItems:
                        [
                            new DemoScheduleItemDefinition(today.AddDays(6), ScheduleItemTypeCatalog.Medicine, "毛玉ケアサプリ", "夕食に混ぜる。", false),
                            new DemoScheduleItemDefinition(today.AddDays(28), ScheduleItemTypeCatalog.Visit, "歯科チェック", "奥歯の歯石を相談。", false)
                        ],
                        Visits:
                        [
                            new DemoVisitDefinition(today.AddDays(-60), "中央ねこ病院", "涙目の相談", "点眼薬", "症状は軽度。")
                        ]),
                    new DemoPetDefinition(
                        Name: "はな",
                        SpeciesCode: "DOG",
                        Breed: "柴犬",
                        Sex: "メス",
                        BirthDate: new DateOnly(2018, 11, 3),
                        AdoptedDate: new DateOnly(2019, 1, 12),
                        IsPublic: true,
                        HealthLogs:
                        [
                            new DemoHealthLogDefinition(today.AddDays(-2), 19, 10, 8.8, 120, 55, "良好", "公園で長めに散歩。"),
                            new DemoHealthLogDefinition(today.AddDays(-8), 6, 50, 8.7, 115, 35, "普通", "朝は食欲控えめ。")
                        ],
                        ScheduleItems:
                        [
                            new DemoScheduleItemDefinition(today.AddDays(9), ScheduleItemTypeCatalog.Vaccine, "狂犬病予防接種", "市の案内はがきを持参。", false),
                            new DemoScheduleItemDefinition(today.AddDays(-3), ScheduleItemTypeCatalog.Other, "シャンプー", "換毛期なのでブラッシング多め。", true)
                        ],
                        Visits:
                        [
                            new DemoVisitDefinition(today.AddDays(-120), "さくら動物医療センター", "皮膚のかゆみ", "抗ヒスタミン薬", "季節性の可能性。")
                        ])
                ]),
            new DemoUserDefinition(
                Email: "demo.tanaka@example.com",
                DisplayName: "田中 健",
                Pets:
                [
                    new DemoPetDefinition(
                        Name: "レオ",
                        SpeciesCode: "DOG",
                        Breed: "ミニチュアダックスフンド",
                        Sex: "オス",
                        BirthDate: new DateOnly(2017, 3, 28),
                        AdoptedDate: new DateOnly(2017, 6, 4),
                        IsPublic: false,
                        HealthLogs:
                        [
                            new DemoHealthLogDefinition(today.AddDays(-1), 20, 40, 5.9, 95, 30, "普通", "段差は避けて散歩。"),
                            new DemoHealthLogDefinition(today.AddDays(-5), 8, 5, 5.9, 90, 20, "良好", "腰の違和感なし。")
                        ],
                        ScheduleItems:
                        [
                            new DemoScheduleItemDefinition(today.AddDays(3), ScheduleItemTypeCatalog.Medicine, "関節サプリ", "朝食後に1粒。", false),
                            new DemoScheduleItemDefinition(today.AddDays(21), ScheduleItemTypeCatalog.Visit, "腰の定期チェック", "歩き方の動画を見せる。", false)
                        ],
                        Visits:
                        [
                            new DemoVisitDefinition(today.AddDays(-35), "北町ペットクリニック", "腰痛の経過観察", "消炎鎮痛薬を短期処方", "体重管理を継続。")
                        ]),
                    new DemoPetDefinition(
                        Name: "モカ",
                        SpeciesCode: "RABBIT",
                        Breed: "ホーランドロップ",
                        Sex: "メス",
                        BirthDate: new DateOnly(2022, 7, 9),
                        AdoptedDate: new DateOnly(2022, 9, 1),
                        IsPublic: true,
                        HealthLogs:
                        [
                            new DemoHealthLogDefinition(today.AddDays(-3), 21, 15, 1.8, 40, null, "良好", "チモシーをよく食べた。")
                        ],
                        ScheduleItems:
                        [
                            new DemoScheduleItemDefinition(today.AddDays(12), ScheduleItemTypeCatalog.Other, "牧草とペレット補充", "同じ銘柄を購入。", false)
                        ],
                        Visits:
                        [
                            new DemoVisitDefinition(today.AddDays(-90), "小動物ホームケア", "健康チェック", null, "歯の伸びは問題なし。")
                        ])
                ])
        ];
    }

    private static DateTime ToDateTime(DateOnly date)
    {
        return date.ToDateTime(TimeOnly.MinValue);
    }

    private static DateTimeOffset ToJstDateTimeOffset(DateOnly date, int hour, int minute)
    {
        return new DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, 0, JapanStandardTimeOffset);
    }

    private static void EnsureIdentitySucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var details = string.Join("; ", result.Errors.Select(x => $"{x.Code}: {x.Description}"));
        throw new InvalidOperationException($"{message} {details}");
    }

    private sealed record DemoUserDefinition(
        string Email,
        string DisplayName,
        IReadOnlyList<DemoPetDefinition> Pets);

    private sealed record DemoPetDefinition(
        string Name,
        string SpeciesCode,
        string? Breed,
        string? Sex,
        DateOnly BirthDate,
        DateOnly AdoptedDate,
        bool IsPublic,
        IReadOnlyList<DemoHealthLogDefinition> HealthLogs,
        IReadOnlyList<DemoScheduleItemDefinition> ScheduleItems,
        IReadOnlyList<DemoVisitDefinition> Visits)
    {
        public Pet CreatePet(string ownerId, DateTimeOffset createdAt)
        {
            var pet = new Pet
            {
                OwnerId = ownerId,
                Name = Name,
                SpeciesCode = SpeciesCode,
                Breed = Breed,
                Sex = Sex,
                BirthDate = ToDateTime(BirthDate),
                AdoptedDate = ToDateTime(AdoptedDate),
                IsPublic = IsPublic,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };

            foreach (var healthLog in HealthLogs)
            {
                pet.HealthLogs.Add(healthLog.CreateHealthLog());
            }

            foreach (var scheduleItem in ScheduleItems)
            {
                pet.ScheduleItems.Add(scheduleItem.CreateScheduleItem(createdAt));
            }

            foreach (var visit in Visits)
            {
                pet.Visits.Add(visit.CreateVisit(createdAt));
            }

            return pet;
        }
    }

    private sealed record DemoHealthLogDefinition(
        DateOnly RecordedDate,
        int Hour,
        int Minute,
        double? WeightKg,
        int? FoodAmountGram,
        int? WalkMinutes,
        string? StoolCondition,
        string? Note)
    {
        public HealthLog CreateHealthLog()
        {
            var recordedAt = ToJstDateTimeOffset(RecordedDate, Hour, Minute);

            return new HealthLog
            {
                RecordedAt = recordedAt,
                CreatedAt = recordedAt,
                UpdatedAt = recordedAt,
                WeightKg = WeightKg,
                FoodAmountGram = FoodAmountGram,
                WalkMinutes = WalkMinutes,
                StoolCondition = StoolCondition,
                Note = Note
            };
        }
    }

    private sealed record DemoScheduleItemDefinition(
        DateOnly DueDate,
        string Type,
        string Title,
        string? Note,
        bool IsDone)
    {
        public ScheduleItem CreateScheduleItem(DateTimeOffset createdAt)
        {
            return new ScheduleItem
            {
                DueDate = ToDateTime(DueDate),
                Type = Type,
                Title = Title,
                Note = Note,
                IsDone = IsDone,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
        }
    }

    private sealed record DemoVisitDefinition(
        DateOnly VisitDate,
        string? ClinicName,
        string? Diagnosis,
        string? Prescription,
        string? Note)
    {
        public Visit CreateVisit(DateTimeOffset createdAt)
        {
            return new Visit
            {
                VisitDate = ToDateTime(VisitDate),
                ClinicName = ClinicName,
                Diagnosis = Diagnosis,
                Prescription = Prescription,
                Note = Note,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
        }
    }
}

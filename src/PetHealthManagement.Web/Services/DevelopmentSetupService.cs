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

        foreach (var definition in BuildDemoPetDefinitions(today))
        {
            var exists = await dbContext.Pets
                .AnyAsync(x => x.OwnerId == adminUser.Id && x.Name == definition.Name, cancellationToken);
            if (exists)
            {
                continue;
            }

            dbContext.Pets.Add(definition.CreatePet(adminUser.Id, now));
            createdPetCount++;
        }

        if (createdPetCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Demo data seed completed. environment={EnvironmentName} createdPetCount={CreatedPetCount}",
            hostEnvironment.EnvironmentName,
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

    private void EnsureDevelopmentEnvironment(string operationName)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            throw new InvalidOperationException($"{operationName} can only run in the Development environment.");
        }
    }

    private static IReadOnlyList<DemoPetDefinition> BuildDemoPetDefinitions(DateOnly today)
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

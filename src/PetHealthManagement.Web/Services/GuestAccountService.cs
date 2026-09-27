using Microsoft.AspNetCore.Identity;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;

namespace PetHealthManagement.Web.Services;

public class GuestAccountService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    ILogger<GuestAccountService> logger) : IGuestAccountService
{
    private static readonly TimeSpan JapanStandardTimeOffset = TimeSpan.FromHours(9);

    public async Task<GuestAccount> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(GuestIdentity.Lifetime);
        var user = new ApplicationUser
        {
            UserName = $"{GuestIdentity.UserNamePrefix}{Guid.NewGuid():N}",
            DisplayName = GuestIdentity.DisplayName
        };

        // claim とサンプルデータを先に追跡しておき、UserManager.CreateAsync の保存でユーザーと一緒に
        // 1 回の SaveChanges で保存する。途中で失敗しても一部だけ残らず、再試行する実行戦略とも両立する。
        dbContext.UserClaims.Add(new IdentityUserClaim<string>
        {
            UserId = user.Id,
            ClaimType = GuestIdentity.ExpiresAtClaimType,
            ClaimValue = GuestIdentity.FormatExpiresAt(expiresAt)
        });

        var createdAt = now.ToOffset(JapanStandardTimeOffset);
        var today = DateOnly.FromDateTime(createdAt.Date);

        // ペット一覧は他の利用者の公開ペットも表示するため、ゲストのペットは非公開で作る
        var pets = DemoPetCatalog.BuildAdminDemoPetDefinitions(today)
            .Select(definition => (definition with { IsPublic = false }).CreatePet(user.Id, createdAt))
            .ToList();
        dbContext.Pets.AddRange(pets);

        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            var details = string.Join("; ", result.Errors.Select(x => $"{x.Code}: {x.Description}"));
            throw new InvalidOperationException($"Failed to create a guest user. {details}");
        }

        logger.LogInformation(
            "Created guest user. userId={UserId} expiresAt={ExpiresAt} petCount={PetCount}",
            user.Id,
            expiresAt,
            pets.Count);

        return new GuestAccount(user, expiresAt);
    }
}

using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Infrastructure;

namespace PetHealthManagement.Web.Services;

public class GuestAccountCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<GuestAccountCleanupService> logger)
{
    public const int BatchSize = 50;

    // Cookie の失効と削除が同時刻にならないよう、期限から少し待ってから削除する
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(5);

    public async Task<GuestCleanupResult> DeleteExpiredGuestsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var expiredGuestIds = await FindExpiredGuestIdsAsync(now, cancellationToken);
        var deletedCount = 0;
        var failedCount = 0;

        foreach (var userId in expiredGuestIds)
        {
            try
            {
                // 削除に失敗したユーザーの変更追跡が次のユーザーの保存に混ざらないよう、1 件ごとにスコープを分ける
                await using var scope = scopeFactory.CreateAsyncScope();
                var userDataDeletionService = scope.ServiceProvider.GetRequiredService<IUserDataDeletionService>();

                if (await userDataDeletionService.DeleteUserAsync(userId, cancellationToken))
                {
                    deletedCount++;
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                failedCount++;
                logger.LogError(ex, "Failed to delete an expired guest user. userId={UserId}", userId);
            }
        }

        return new GuestCleanupResult(expiredGuestIds.Count, deletedCount, failedCount);
    }

    private async Task<IReadOnlyList<string>> FindExpiredGuestIdsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // ゲストの作成はレート制限されていて件数が少ないため、期限の解釈は DB ではなくアプリ側で行う
        var guestClaims = await dbContext.UserClaims
            .AsNoTracking()
            .Where(x => x.ClaimType == GuestIdentity.ExpiresAtClaimType)
            .Select(x => new { x.UserId, x.ClaimValue })
            .ToListAsync(cancellationToken);

        if (guestClaims.Count == 0)
        {
            return [];
        }

        var adminUserIds = (await (
                from userRole in dbContext.UserRoles
                join role in dbContext.Roles on userRole.RoleId equals role.Id
                where role.Name == DevelopmentSetupService.AdminRoleName
                select userRole.UserId)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var deleteBefore = now - GracePeriod;
        var expiredGuests = new List<(string UserId, DateTimeOffset ExpiresAt)>();

        foreach (var claim in guestClaims)
        {
            if (!GuestIdentity.TryParseExpiresAt(claim.ClaimValue, out var expiresAt))
            {
                logger.LogWarning(
                    "Skipped a guest user whose expiry claim could not be read. userId={UserId}",
                    claim.UserId);
                continue;
            }

            if (expiresAt > deleteBefore)
            {
                continue;
            }

            if (adminUserIds.Contains(claim.UserId))
            {
                logger.LogWarning(
                    "Skipped an expired guest user in the Admin role. userId={UserId}",
                    claim.UserId);
                continue;
            }

            expiredGuests.Add((claim.UserId, expiresAt));
        }

        return expiredGuests
            .OrderBy(x => x.ExpiresAt)
            .Select(x => x.UserId)
            .Distinct(StringComparer.Ordinal)
            .Take(BatchSize)
            .ToList();
    }
}

public sealed record GuestCleanupResult(int ExpiredCount, int DeletedCount, int FailedCount);

using System.Globalization;
using System.Security.Claims;

namespace PetHealthManagement.Web.Infrastructure;

public static class GuestIdentity
{
    // ゲストは有効期限を値に持つこの claim で見分ける。本番の Migration は手動適用のため、列は追加しない
    public const string ExpiresAtClaimType = "pethealth:guest-expires-at";

    // 認証済みで、ゲストではないユーザーだけを通す認可ポリシー
    public const string NonGuestPolicyName = "NonGuest";

    public const string DisplayName = "ゲスト";

    public const string UserNamePrefix = "guest-";

    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    public static bool IsGuest(ClaimsPrincipal principal)
    {
        return principal.Identity?.IsAuthenticated == true
               && principal.HasClaim(claim => claim.Type == ExpiresAtClaimType);
    }

    public static DateTimeOffset? FindExpiresAt(ClaimsPrincipal principal)
    {
        return IsGuest(principal)
               && TryParseExpiresAt(principal.FindFirstValue(ExpiresAtClaimType), out var expiresAt)
            ? expiresAt
            : null;
    }

    public static string FormatExpiresAt(DateTimeOffset expiresAt)
    {
        return expiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    public static bool TryParseExpiresAt(string? value, out DateTimeOffset expiresAt)
    {
        return DateTimeOffset.TryParseExact(
            value,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out expiresAt);
    }
}

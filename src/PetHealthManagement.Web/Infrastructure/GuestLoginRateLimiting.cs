using System.Threading.RateLimiting;

namespace PetHealthManagement.Web.Infrastructure;

public static class GuestLoginRateLimiting
{
    public const string PolicyName = "GuestLogin";
    public const int PermitLimit = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public static Func<HttpContext, RateLimitPartition<string>> BuildPolicy()
    {
        return context => RateLimitPartition.GetFixedWindowLimiter(
            ResolvePartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitLimit,
                Window = Window,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }

    // ゲストは未ログインの状態で作るため、ユーザーではなく接続元の IP アドレスで数える
    public static string ResolvePartitionKey(HttpContext context)
    {
        var remoteIpAddress = context.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(remoteIpAddress)
            ? "ip:unknown"
            : $"ip:{remoteIpAddress}";
    }
}

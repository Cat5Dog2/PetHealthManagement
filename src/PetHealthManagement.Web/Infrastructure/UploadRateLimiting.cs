using System.Security.Claims;
using System.Threading.RateLimiting;

namespace PetHealthManagement.Web.Infrastructure;

public static class UploadRateLimiting
{
    public const string ImageUploadPolicyName = "ImageUploads";
    public const int PermitLimit = 6;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static Func<HttpContext, RateLimitPartition<string>> BuildImageUploadPolicy()
    {
        return context =>
        {
            var partitionKey = ResolvePartitionKey(context);

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitLimit,
                    Window = Window,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        };
    }

    public static string ResolvePartitionKey(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        var remoteIpAddress = context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(remoteIpAddress))
        {
            return $"ip:{remoteIpAddress}";
        }

        return "ip:unknown";
    }
}

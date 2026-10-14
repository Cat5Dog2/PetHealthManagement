using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Infrastructure;

public sealed class GuestAccountCleanupConnectionInterceptor(
    IHttpContextAccessor httpContextAccessor,
    GuestAccountCleanupQueue cleanupQueue) : DbConnectionInterceptor
{
    public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData)
    {
        RequestCleanup();
    }

    public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
    {
        RequestCleanup();
        return Task.CompletedTask;
    }

    private void RequestCleanup()
    {
        // 利用者の DB 接続だけを契機にする。整理自身や CLI の接続では再通知しない。
        if (httpContextAccessor.HttpContext is not null)
        {
            cleanupQueue.TryRequestCleanup();
        }
    }
}

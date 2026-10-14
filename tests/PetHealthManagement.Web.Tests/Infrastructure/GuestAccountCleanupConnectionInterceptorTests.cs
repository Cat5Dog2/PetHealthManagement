using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Tests.Infrastructure;

public sealed class GuestAccountCleanupConnectionInterceptorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DatabaseUse_TriggersCleanup_OnlyWithinHttpRequest(bool inRequest, bool asyncQuery)
    {
        var httpContextAccessor = new HttpContextAccessor();
        var queue = new GuestAccountCleanupQueue(TimeProvider.System, Options.Create(new GuestLoginOptions()));
        var interceptor = new GuestAccountCleanupConnectionInterceptor(httpContextAccessor, queue);
        await using var keeper = new SqliteConnection($"Data Source=cleanup-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(keeper.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        using var cancellation = new CancellationTokenSource();
        await using var requests = queue.ReadAllAsync(cancellation.Token).GetAsyncEnumerator();
        var pendingRead = requests.MoveNextAsync().AsTask();
        httpContextAccessor.HttpContext = inRequest ? new DefaultHttpContext() : null;
        try
        {
            if (asyncQuery)
            {
                Assert.Equal(0, await dbContext.Users.CountAsync());
            }
            else
            {
                Assert.Equal(0, dbContext.Users.Count());
            }

            if (inRequest)
            {
                Assert.True(await pendingRead.WaitAsync(TimeSpan.FromSeconds(5)));
            }
            else
            {
                Assert.False(pendingRead.IsCompleted);
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pendingRead);
            }
        }
        finally
        {
            httpContextAccessor.HttpContext = null;
        }
    }
}

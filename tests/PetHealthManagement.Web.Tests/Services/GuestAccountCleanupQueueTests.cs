using Microsoft.Extensions.Options;
using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Tests.Services;

public sealed class GuestAccountCleanupQueueTests
{
    [Fact]
    public async Task Requests_AreCoalesced_AndWaitForNewActivityAfterCooldown()
    {
        var clock = new MutableTimeProvider();
        var queue = new GuestAccountCleanupQueue(clock, Options.Create(new GuestLoginOptions()));
        using var cancellation = new CancellationTokenSource();
        await using var requests = queue.ReadAllAsync(cancellation.Token).GetAsyncEnumerator();
        var pendingRead = requests.MoveNextAsync().AsTask();

        // 時刻が進むだけでは整理を開始しない。
        clock.UtcNow += TimeSpan.FromDays(1);
        Assert.False(pendingRead.IsCompleted);

        var accepted = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(queue.TryRequestCleanup)));
        Assert.Single(accepted, result => result);
        Assert.True(await pendingRead.WaitAsync(TimeSpan.FromSeconds(5)));

        // 整理に時間が掛かっても、実行中に追加の整理を予約しない。
        clock.UtcNow += GuestAccountCleanupWorker.Interval;
        Assert.False(queue.TryRequestCleanup());
        queue.CompleteCleanup();

        pendingRead = requests.MoveNextAsync().AsTask();
        clock.UtcNow += GuestAccountCleanupWorker.Interval - TimeSpan.FromTicks(1);
        Assert.False(queue.TryRequestCleanup());
        clock.UtcNow += TimeSpan.FromTicks(1);
        Assert.False(pendingRead.IsCompleted);
        Assert.True(queue.TryRequestCleanup());
        Assert.True(await pendingRead.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task DisabledCleanup_DoesNotQueueWork()
    {
        var queue = new GuestAccountCleanupQueue(new MutableTimeProvider(),
            Options.Create(new GuestLoginOptions { CleanupEnabled = false }));
        using var cancellation = new CancellationTokenSource();
        await using var requests = queue.ReadAllAsync(cancellation.Token).GetAsyncEnumerator();
        var pendingRead = requests.MoveNextAsync().AsTask();

        Assert.False(queue.TryRequestCleanup());
        Assert.False(pendingRead.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pendingRead);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}

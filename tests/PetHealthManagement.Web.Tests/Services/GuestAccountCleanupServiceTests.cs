using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Services;

// 本番と同じく再試行する実行戦略を設定した SQLite で、ゲストの作成と期限切れゲストの削除を確認する
public sealed class GuestAccountCleanupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    // 期限（作成 + 8 時間）に猶予を加えた時刻を過ぎている
    private static readonly DateTimeOffset ExpiredCreatedAt =
        Now - GuestIdentity.Lifetime - GuestAccountCleanupService.GracePeriod - TimeSpan.FromSeconds(1);

    [Fact]
    public async Task DeleteExpiredGuestsAsync_DeletesExpiredGuestWithDataAndImageFiles_AndKeepsOtherUsers()
    {
        await using var context = await CleanupTestContext.CreateAsync();
        var expiredGuestId = await context.CreateGuestAsync(ExpiredCreatedAt);
        var guestWithinGracePeriodId = await context.CreateGuestAsync(Now - GuestIdentity.Lifetime - TimeSpan.FromMinutes(1));
        var activeGuestId = await context.CreateGuestAsync(Now - TimeSpan.FromHours(1));
        await context.SeedRegisteredUserWithPetAsync("registered-user");
        var storageKey = await context.AttachHealthLogImageAsync(expiredGuestId);
        Assert.True(File.Exists(context.GetStoragePath(storageKey)));

        var result = await context.CleanupService.DeleteExpiredGuestsAsync(Now);

        Assert.Equal(new GuestCleanupResult(ExpiredCount: 1, DeletedCount: 1, FailedCount: 0), result);
        Assert.False(File.Exists(context.GetStoragePath(storageKey)));

        await using var scope = context.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await dbContext.Users.AnyAsync(x => x.Id == expiredGuestId));
        Assert.False(await dbContext.UserClaims.AnyAsync(x => x.UserId == expiredGuestId));
        Assert.False(await dbContext.Pets.AnyAsync(x => x.OwnerId == expiredGuestId));
        Assert.False(await dbContext.ImageAssets.AnyAsync(x => x.OwnerId == expiredGuestId));
        Assert.Equal(0, await dbContext.HealthLogImages.CountAsync());

        // 残る 2 人のゲストのサンプルデータ（1 人あたりペット 3・健康ログ 7・予定 7・通院 4）と登録ユーザーのデータ
        Assert.Equal(
            new[] { activeGuestId, guestWithinGracePeriodId, "registered-user" }.Order(StringComparer.Ordinal).ToArray(),
            (await dbContext.Users.Select(x => x.Id).ToListAsync()).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(7, await dbContext.Pets.CountAsync());
        Assert.Equal(14, await dbContext.HealthLogs.CountAsync());
        Assert.Equal(14, await dbContext.ScheduleItems.CountAsync());
        Assert.Equal(8, await dbContext.Visits.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_SavesGuestClaimAndPrivateSamplePets()
    {
        await using var context = await CleanupTestContext.CreateAsync();

        var guestId = await context.CreateGuestAsync(Now);

        await using var scope = context.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var guest = await dbContext.Users.SingleAsync(x => x.Id == guestId);
        var claim = await dbContext.UserClaims.SingleAsync(x => x.UserId == guestId);
        var pets = await dbContext.Pets.Where(x => x.OwnerId == guestId).ToListAsync();

        Assert.StartsWith(GuestIdentity.UserNamePrefix, guest.UserName, StringComparison.Ordinal);
        Assert.Equal(GuestIdentity.DisplayName, guest.DisplayName);
        Assert.Null(guest.Email);
        Assert.Equal(GuestIdentity.ExpiresAtClaimType, claim.ClaimType);
        Assert.True(GuestIdentity.TryParseExpiresAt(claim.ClaimValue, out var expiresAt));
        Assert.Equal(Now + GuestIdentity.Lifetime, expiresAt);
        Assert.Equal(3, pets.Count);
        Assert.All(pets, pet => Assert.False(pet.IsPublic));
    }

    [Fact]
    public async Task DeleteExpiredGuestsAsync_KeepsAdminAndUnreadableExpiry_AndLogsWarnings()
    {
        await using var context = await CleanupTestContext.CreateAsync();
        var adminGuestId = await context.CreateGuestAsync(ExpiredCreatedAt);
        await context.AddToAdminRoleAsync(adminGuestId);
        await context.SeedUserWithExpiryClaimAsync("broken-guest", "not-a-date");

        var result = await context.CleanupService.DeleteExpiredGuestsAsync(Now);

        Assert.Equal(new GuestCleanupResult(ExpiredCount: 0, DeletedCount: 0, FailedCount: 0), result);
        await using var scope = context.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await dbContext.Users.AnyAsync(x => x.Id == adminGuestId));
        Assert.True(await dbContext.Users.AnyAsync(x => x.Id == "broken-guest"));
        Assert.Contains(context.CleanupLogger.Entries, entry => entry.LogLevel == LogLevel.Warning
                                                                && Equals(entry.Properties["UserId"], adminGuestId));
        Assert.Contains(context.CleanupLogger.Entries, entry => entry.LogLevel == LogLevel.Warning
                                                                && Equals(entry.Properties["UserId"], "broken-guest"));
    }

    [Fact]
    public async Task DeleteExpiredGuestsAsync_LogsFailureAndContinues_WhenOneDeletionFails()
    {
        var commitFailures = new CommitFailureInterceptor();
        await using var context = await CleanupTestContext.CreateAsync(commitFailures);
        var olderGuestId = await context.CreateGuestAsync(ExpiredCreatedAt - TimeSpan.FromMinutes(10));
        var newerGuestId = await context.CreateGuestAsync(ExpiredCreatedAt);
        var failure = new InvalidOperationException("Simulated commit failure.");
        commitFailures.FailNextCommitWith(failure);

        var result = await context.CleanupService.DeleteExpiredGuestsAsync(Now);

        Assert.Equal(new GuestCleanupResult(ExpiredCount: 2, DeletedCount: 1, FailedCount: 1), result);
        await using var scope = context.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await dbContext.Users.AnyAsync(x => x.Id == olderGuestId));
        Assert.Equal(3, await dbContext.Pets.CountAsync(x => x.OwnerId == olderGuestId));
        Assert.False(await dbContext.Users.AnyAsync(x => x.Id == newerGuestId));
        var errorLog = Assert.Single(context.CleanupLogger.Entries, entry => entry.LogLevel == LogLevel.Error);
        Assert.Same(failure, errorLog.Exception);
        Assert.Equal(olderGuestId, errorLog.Properties["UserId"]);
    }

    [Fact]
    public async Task DeleteExpiredGuestsAsync_DeletesAtMostBatchSize_OldestFirst()
    {
        await using var context = await CleanupTestContext.CreateAsync();
        var expiredGuestCount = GuestAccountCleanupService.BatchSize + 1;
        for (var index = 0; index < expiredGuestCount; index++)
        {
            var expiresAt = ExpiredCreatedAt + GuestIdentity.Lifetime - TimeSpan.FromMinutes(expiredGuestCount - index);
            await context.SeedUserWithExpiryClaimAsync($"guest-{index:D2}", GuestIdentity.FormatExpiresAt(expiresAt));
        }

        var result = await context.CleanupService.DeleteExpiredGuestsAsync(Now);

        Assert.Equal(
            new GuestCleanupResult(
                ExpiredCount: GuestAccountCleanupService.BatchSize,
                DeletedCount: GuestAccountCleanupService.BatchSize,
                FailedCount: 0),
            result);
        await using var scope = context.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal($"guest-{expiredGuestCount - 1:D2}", Assert.Single(await dbContext.Users.Select(x => x.Id).ToListAsync()));
    }

    [Fact]
    public async Task CleanupWorker_DeletesExpiredGuestsOnStartup()
    {
        await using var context = await CleanupTestContext.CreateAsync();
        var expiredGuestId = await context.CreateGuestAsync(ExpiredCreatedAt);
        var workerLogger = new CleanupRunLogger();
        using var worker = new GuestAccountCleanupWorker(
            context.CleanupService,
            Options.Create(new GuestLoginOptions { CleanupEnabled = true }),
            context.TimeProvider,
            workerLogger);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            var message = await workerLogger.FirstRun.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.StartsWith("Expired guest cleanup completed.", message, StringComparison.Ordinal);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        await using var scope = context.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await dbContext.Users.AnyAsync(x => x.Id == expiredGuestId));
    }

    [Fact]
    public async Task CleanupWorker_DoesNothing_WhenCleanupIsDisabled()
    {
        await using var context = await CleanupTestContext.CreateAsync();
        var expiredGuestId = await context.CreateGuestAsync(ExpiredCreatedAt);
        var workerLogger = new CleanupRunLogger();
        using var worker = new GuestAccountCleanupWorker(
            context.CleanupService,
            Options.Create(new GuestLoginOptions { CleanupEnabled = false }),
            context.TimeProvider,
            workerLogger);

        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal("Expired guest cleanup is disabled.", Assert.Single(workerLogger.Messages));
        await using var scope = context.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await dbContext.Users.AnyAsync(x => x.Id == expiredGuestId));
    }

    private sealed class CleanupTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;
        private readonly TestFileBackedImageStorageService _storage;

        private CleanupTestContext(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            TestFileBackedImageStorageService storage,
            MutableTimeProvider timeProvider)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            _storage = storage;
            TimeProvider = timeProvider;
            CleanupService = new GuestAccountCleanupService(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                CleanupLogger);
        }

        public MutableTimeProvider TimeProvider { get; }

        public TestLogger<GuestAccountCleanupService> CleanupLogger { get; } = new();

        public GuestAccountCleanupService CleanupService { get; }

        public static async Task<CleanupTestContext> CreateAsync(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var storage = new TestFileBackedImageStorageService("PetHealthManagement.GuestCleanupTests");
            var timeProvider = new MutableTimeProvider(Now);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options =>
                TestDbContextFactory.UseRetryingSqlite(options, connection, interceptors));
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddSingleton<TimeProvider>(timeProvider);
            services.AddSingleton<IImageStorageService>(storage);
            services.AddScoped<IUserDataDeletionService, UserDataDeletionService>();
            services.AddScoped<IGuestAccountService, GuestAccountService>();

            var serviceProvider = services.BuildServiceProvider(validateScopes: true);
            await using (var scope = serviceProvider.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
            }

            return new CleanupTestContext(connection, serviceProvider, storage, timeProvider);
        }

        public AsyncServiceScope CreateScope()
        {
            return _serviceProvider.CreateAsyncScope();
        }

        public async Task<string> CreateGuestAsync(DateTimeOffset createdAt)
        {
            TimeProvider.UtcNow = createdAt;
            await using var scope = CreateScope();
            var guest = await scope.ServiceProvider.GetRequiredService<IGuestAccountService>().CreateAsync();
            TimeProvider.UtcNow = Now;
            return guest.User.Id;
        }

        public async Task SeedUserWithExpiryClaimAsync(string userId, string claimValue)
        {
            await using var scope = CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Users.Add(new ApplicationUser { Id = userId, UserName = userId });
            dbContext.UserClaims.Add(new IdentityUserClaim<string>
            {
                UserId = userId,
                ClaimType = GuestIdentity.ExpiresAtClaimType,
                ClaimValue = claimValue
            });
            await dbContext.SaveChangesAsync();
        }

        public async Task SeedRegisteredUserWithPetAsync(string userId)
        {
            await using var scope = CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Users.Add(new ApplicationUser { Id = userId, UserName = userId, Email = $"{userId}@example.com" });
            dbContext.Pets.Add(new Pet
            {
                OwnerId = userId,
                Name = "Registered Pet",
                SpeciesCode = "CAT",
                CreatedAt = Now,
                UpdatedAt = Now
            });
            await dbContext.SaveChangesAsync();
        }

        public async Task AddToAdminRoleAsync(string userId)
        {
            await using var scope = CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await roleManager.CreateAsync(new IdentityRole(DevelopmentSetupService.AdminRoleName))).Succeeded);
            var user = await userManager.FindByIdAsync(userId);
            Assert.NotNull(user);
            Assert.True((await userManager.AddToRoleAsync(user, DevelopmentSetupService.AdminRoleName)).Succeeded);
        }

        // ゲストの健康ログに画像を 1 枚付け、ストレージにもファイルを置く
        public async Task<string> AttachHealthLogImageAsync(string guestId)
        {
            await using var scope = CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var healthLog = await dbContext.HealthLogs
                .Where(x => x.Pet.OwnerId == guestId)
                .OrderBy(x => x.Id)
                .FirstAsync();
            var imageId = Guid.NewGuid();
            var storageKey = $"images/{imageId:N}.png";

            dbContext.ImageAssets.Add(new ImageAsset
            {
                ImageId = imageId,
                StorageKey = storageKey,
                ContentType = "image/png",
                SizeBytes = 4,
                OwnerId = guestId,
                Category = "HealthLog",
                Status = ImageAssetStatus.Ready,
                CreatedAt = Now
            });
            dbContext.HealthLogImages.Add(new HealthLogImage { HealthLogId = healthLog.Id, ImageId = imageId, SortOrder = 1 });
            await dbContext.SaveChangesAsync();

            var path = GetStoragePath(storageKey);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
            return storageKey;
        }

        public string GetStoragePath(string storageKey)
        {
            return Path.Combine(_storage.RootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));
        }

        public async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync();
            await _connection.DisposeAsync();
            _storage.Dispose();
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return UtcNow;
        }
    }

    // ワーカーの 1 回目の実行結果（完了または失敗のログ）を待てるようにする
    private sealed class CleanupRunLogger : ILogger<GuestAccountCleanupWorker>
    {
        public TaskCompletionSource<string> FirstRun { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            _ = state;
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            _ = logLevel;
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            lock (Messages)
            {
                Messages.Add(message);
            }

            if (message.StartsWith("Expired guest cleanup completed.", StringComparison.Ordinal)
                || message.StartsWith("Expired guest cleanup failed.", StringComparison.Ordinal))
            {
                FirstRun.TrySetResult(message);
            }
        }
    }
}

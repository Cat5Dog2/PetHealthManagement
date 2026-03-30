using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Services;

public class UserDataDeletionServiceTests
{
    [Fact]
    public async Task DeleteUserAsync_RemovesOwnedUserPetsAndRelatedDataAndImages()
    {
        await using var dbContext = CreateDbContext();

        dbContext.Users.AddRange(
            new ApplicationUser { Id = "user-a", UserName = "userA", AvatarImageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") },
            new ApplicationUser { Id = "user-b", UserName = "userB" });

        dbContext.Pets.AddRange(
            new Pet
            {
                Id = 1,
                OwnerId = "user-a",
                Name = "Mugi",
                SpeciesCode = "DOG",
                PhotoImageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Pet
            {
                Id = 2,
                OwnerId = "user-b",
                Name = "Sora",
                SpeciesCode = "CAT",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        dbContext.ImageAssets.AddRange(
            NewImageAsset(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "user-a", "Avatar", "images/avatar-a.jpg"),
            NewImageAsset(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "user-a", "PetPhoto", "images/pet-a.jpg"),
            NewImageAsset(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "user-a", "HealthLog", "images/log-a.jpg"),
            NewImageAsset(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "user-a", "Visit", "images/visit-a.jpg"),
            NewImageAsset(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "user-b", "Avatar", "images/avatar-b.jpg"));

        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.Visits.Add(new Visit
        {
            Id = 20,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 24),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.ScheduleItems.AddRange(
            new ScheduleItem
            {
                Id = 30,
                PetId = 1,
                DueDate = new DateTime(2026, 3, 28),
                Type = ScheduleItemTypeCatalog.Vaccine,
                Title = "Rabies",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new ScheduleItem
            {
                Id = 31,
                PetId = 2,
                DueDate = new DateTime(2026, 4, 1),
                Type = ScheduleItemTypeCatalog.Other,
                Title = "Reminder",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        dbContext.HealthLogImages.Add(new HealthLogImage
        {
            Id = 1,
            HealthLogId = 10,
            ImageId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            SortOrder = 1
        });

        dbContext.VisitImages.Add(new VisitImage
        {
            Id = 1,
            VisitId = 20,
            ImageId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            SortOrder = 1
        });

        await dbContext.SaveChangesAsync();

        var storage = new FakeImageStorageService();
        var logger = new TestLogger<UserDataDeletionService>();
        var service = new UserDataDeletionService(dbContext, storage, logger);

        var deleted = await service.DeleteUserAsync("user-a");

        Assert.True(deleted);
        Assert.Null(await dbContext.Users.SingleOrDefaultAsync(x => x.Id == "user-a"));
        Assert.Equal(1, await dbContext.Users.CountAsync());
        Assert.Equal(1, await dbContext.Pets.CountAsync());
        Assert.Equal(0, await dbContext.HealthLogs.CountAsync());
        Assert.Equal(0, await dbContext.HealthLogImages.CountAsync());
        Assert.Equal(0, await dbContext.Visits.CountAsync());
        Assert.Equal(0, await dbContext.VisitImages.CountAsync());
        Assert.Equal(1, await dbContext.ScheduleItems.CountAsync());
        Assert.Equal(1, await dbContext.ImageAssets.CountAsync());
        Assert.Equal(
            ["images/avatar-a.jpg", "images/log-a.jpg", "images/pet-a.jpg", "images/visit-a.jpg"],
            storage.DeletedStorageKeys.OrderBy(x => x).ToArray());
        Assert.Collection(
            logger.Entries.Where(x => x.LogLevel == LogLevel.Information).ToArray(),
            entry =>
            {
                Assert.Equal(ApplicationOperationLogging.Operations.DeleteUserData, entry.Properties["Operation"]);
                Assert.Equal("user-a", entry.Properties["OwnerId"]);
                Assert.Equal("User", entry.Properties["TargetType"]);
                Assert.Equal("user-a", entry.Properties["TargetId"]);
                Assert.Equal(1, entry.Properties["PetCount"]);
                Assert.Equal(1, entry.Properties["HealthLogCount"]);
                Assert.Equal(1, entry.Properties["VisitCount"]);
                Assert.Equal(1, entry.Properties["ScheduleItemCount"]);
                Assert.Equal(4, entry.Properties["ImageAssetCount"]);
                Assert.Equal(4, entry.Properties["StorageTargetCount"]);
            },
            entry =>
            {
                Assert.Equal(ApplicationOperationLogging.Operations.DeleteUserData, entry.Properties["Operation"]);
                Assert.Equal("user-a", entry.Properties["OwnerId"]);
                Assert.Equal("User", entry.Properties["TargetType"]);
                Assert.Equal("user-a", entry.Properties["TargetId"]);
                Assert.Equal(1, entry.Properties["PetCount"]);
                Assert.Equal(1, entry.Properties["HealthLogCount"]);
                Assert.Equal(1, entry.Properties["VisitCount"]);
                Assert.Equal(1, entry.Properties["ScheduleItemCount"]);
                Assert.Equal(4, entry.Properties["ImageAssetCount"]);
                Assert.Equal(4, entry.Properties["StorageTargetCount"]);
            });
    }

    [Fact]
    public async Task DeleteUserAsync_Continues_WhenImageDeletionFails()
    {
        await using var dbContext = CreateDbContext();

        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.ImageAssets.Add(NewImageAsset(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            "user-a",
            "Avatar",
            "images/fail-delete.jpg"));

        await dbContext.SaveChangesAsync();

        var storage = new FakeImageStorageService
        {
            FailingStorageKeys = ["images/fail-delete.jpg"]
        };

        var logger = new TestLogger<UserDataDeletionService>();
        var service = new UserDataDeletionService(dbContext, storage, logger);

        var deleted = await service.DeleteUserAsync("user-a");

        Assert.True(deleted);
        Assert.Empty(await dbContext.Users.ToListAsync());
        Assert.Empty(await dbContext.ImageAssets.ToListAsync());
        Assert.Contains(
            logger.Entries,
            entry => entry.LogLevel == LogLevel.Warning
                     && Equals(entry.Properties["OwnerId"], "user-a")
                     && Equals(entry.Properties["ResourceType"], "User")
                     && Equals(entry.Properties["ResourceId"], "user-a"));
    }

    [Fact]
    public async Task DeleteUserAsync_ReturnsFalse_WhenUserDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var logger = new TestLogger<UserDataDeletionService>();
        var service = new UserDataDeletionService(dbContext, new FakeImageStorageService(), logger);

        var deleted = await service.DeleteUserAsync("missing-user");

        Assert.False(deleted);
        var warningLog = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warningLog.LogLevel);
        Assert.Equal(ApplicationOperationLogging.Operations.DeleteUserData, warningLog.Properties["Operation"]);
        Assert.Equal("missing-user", warningLog.Properties["OwnerId"]);
        Assert.Equal("User", warningLog.Properties["TargetType"]);
        Assert.Equal("missing-user", warningLog.Properties["TargetId"]);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"user-deletion-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ImageAsset NewImageAsset(Guid imageId, string ownerId, string category, string storageKey)
    {
        return new ImageAsset
        {
            ImageId = imageId,
            StorageKey = storageKey,
            ContentType = "image/jpeg",
            SizeBytes = 128,
            OwnerId = ownerId,
            Category = category,
            Status = ImageAssetStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class FakeImageStorageService : IImageStorageService
    {
        public HashSet<string> FailingStorageKeys { get; init; } = [];

        public List<string> DeletedStorageKeys { get; } = [];

        public string CreateTemporaryPath(string extension)
        {
            throw new NotSupportedException();
        }

        public Task SaveFormFileToPathAsync(IFormFile file, string destinationPath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task MoveToStorageAsync(string sourcePath, string storageKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            DeletedStorageKeys.Add(storageKey);

            if (FailingStorageKeys.Contains(storageKey))
            {
                throw new IOException("Simulated delete failure.");
            }

            return Task.CompletedTask;
        }
    }
}

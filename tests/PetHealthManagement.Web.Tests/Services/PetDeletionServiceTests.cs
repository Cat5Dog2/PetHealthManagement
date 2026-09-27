using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Services;

public class PetDeletionServiceTests
{
    [Fact]
    public async Task DeleteAsync_RemovesPetRelatedDataAndImages_AndUpdatesUsedBytes()
    {
        await using var dbContext = CreateDbContext();
        var storage = new FakeImageStorageService();

        dbContext.Users.Add(new ApplicationUser
        {
            Id = "user-a",
            UserName = "userA",
            UsedImageBytes = 790
        });

        dbContext.Pets.AddRange(
            NewPet(1, "user-a", "Mugi", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            NewPet(2, "user-a", "Sora", null));

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
            VisitDate = new DateTime(2026, 3, 22),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.ScheduleItems.AddRange(
            new ScheduleItem
            {
                Id = 30,
                PetId = 1,
                DueDate = new DateTime(2026, 3, 30),
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

        var petPhotoImageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var healthLogImageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var visitImageId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var avatarImageId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        dbContext.ImageAssets.AddRange(
            NewImageAsset(petPhotoImageId, "user-a", "PetPhoto", "images/pet-1.jpg", 200),
            NewImageAsset(healthLogImageId, "user-a", "HealthLog", "images/log-1.jpg", 120),
            NewImageAsset(visitImageId, "user-a", "Visit", "images/visit-1.jpg", 150),
            NewImageAsset(avatarImageId, "user-a", "Avatar", "images/avatar-1.jpg", 320));

        dbContext.HealthLogImages.Add(new HealthLogImage
        {
            Id = 1,
            HealthLogId = 10,
            ImageId = healthLogImageId,
            SortOrder = 1
        });

        dbContext.VisitImages.Add(new VisitImage
        {
            Id = 1,
            VisitId = 20,
            ImageId = visitImageId,
            SortOrder = 1
        });

        await dbContext.SaveChangesAsync();

        var service = new PetDeletionService(dbContext, storage, NullLogger<PetDeletionService>.Instance);
        var pet = await dbContext.Pets.SingleAsync(x => x.Id == 1);

        await service.DeleteAsync(pet, "user-a");

        var owner = await dbContext.Users.SingleAsync(x => x.Id == "user-a");

        Assert.Equal(1, await dbContext.Pets.CountAsync());
        Assert.Equal(2, (await dbContext.Pets.SingleAsync()).Id);
        Assert.Empty(await dbContext.HealthLogs.ToListAsync());
        Assert.Empty(await dbContext.HealthLogImages.ToListAsync());
        Assert.Empty(await dbContext.Visits.ToListAsync());
        Assert.Empty(await dbContext.VisitImages.ToListAsync());
        Assert.Single(await dbContext.ScheduleItems.ToListAsync());
        Assert.Single(await dbContext.ImageAssets.ToListAsync());
        Assert.Equal(320, owner.UsedImageBytes);
        Assert.Equal(
            ["images/log-1.jpg", "images/pet-1.jpg", "images/visit-1.jpg"],
            storage.DeletedStorageKeys.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task DeleteAsync_Continues_WhenFileDeletionFails()
    {
        await using var dbContext = CreateDbContext();
        var storage = new FakeImageStorageService
        {
            FailingStorageKeys = ["images/pet-1.jpg"]
        };
        var logger = new TestLogger<PetDeletionService>();

        dbContext.Users.Add(new ApplicationUser
        {
            Id = "user-a",
            UserName = "userA",
            UsedImageBytes = 200
        });

        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
        dbContext.ImageAssets.Add(NewImageAsset(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "user-a",
            "PetPhoto",
            "images/pet-1.jpg",
            200));

        await dbContext.SaveChangesAsync();

        var service = new PetDeletionService(dbContext, storage, logger);
        var pet = await dbContext.Pets.SingleAsync(x => x.Id == 1);

        await service.DeleteAsync(pet, "user-a");

        var owner = await dbContext.Users.SingleAsync(x => x.Id == "user-a");

        Assert.Empty(await dbContext.Pets.ToListAsync());
        Assert.Empty(await dbContext.ImageAssets.ToListAsync());
        Assert.Equal(0, owner.UsedImageBytes);
        Assert.Contains("images/pet-1.jpg", storage.DeletedStorageKeys);
        Assert.Equal(3, logger.Entries.Count);
        var startedLog = logger.Entries[0];
        Assert.Equal(LogLevel.Information, startedLog.LogLevel);
        Assert.Equal(ApplicationOperationLogging.Operations.DeletePet, startedLog.Properties["Operation"]);
        Assert.Equal("user-a", startedLog.Properties["OwnerId"]);
        Assert.Equal("Pet", startedLog.Properties["TargetType"]);
        Assert.Equal(1, startedLog.Properties["TargetId"]);

        var completedLog = logger.Entries[1];
        Assert.Equal(LogLevel.Information, completedLog.LogLevel);
        Assert.Equal(ApplicationOperationLogging.Operations.DeletePet, completedLog.Properties["Operation"]);
        Assert.Equal("user-a", completedLog.Properties["OwnerId"]);
        Assert.Equal("Pet", completedLog.Properties["TargetType"]);
        Assert.Equal(1, completedLog.Properties["TargetId"]);

        var warningLog = logger.Entries[2];
        Assert.Equal(LogLevel.Warning, warningLog.LogLevel);
        Assert.Equal("PetPhoto", warningLog.Properties["ImageCategory"]);
        Assert.Equal("user-a", warningLog.Properties["OwnerId"]);
        Assert.Equal("Pet", warningLog.Properties["ResourceType"]);
        Assert.Equal(1, warningLog.Properties["ResourceId"]);
        Assert.Equal(ImageOperationLogging.Phases.CascadeDelete, warningLog.Properties["Phase"]);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), warningLog.Properties["ImageId"]);
        Assert.Equal("images/pet-1.jpg", warningLog.Properties["StorageKey"]);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPetRelatedData_WithRetryingExecutionStrategy()
    {
        await using var testContext = await TestDbContextFactory.CreateRetryingSqliteInMemoryContextAsync();
        var dbContext = testContext.DbContext;
        await SeedPetWithRelatedDataAsync(dbContext);

        var storage = new FakeImageStorageService();
        var service = new PetDeletionService(dbContext, storage, NullLogger<PetDeletionService>.Instance);
        var pet = await dbContext.Pets.SingleAsync(x => x.Id == 1);

        await service.DeleteAsync(pet, "user-a");

        await AssertOnlyOtherPetDataRemainsAsync(dbContext, expectedUsedImageBytes: 320);
        Assert.Equal(
            ["images/log-1.jpg", "images/pet-1.jpg", "images/visit-1.jpg"],
            storage.DeletedStorageKeys.Order().ToArray());
    }

    [Fact]
    public async Task DeleteAsync_RetriesTransactionWithoutDoubleCountingBytes_WhenCommitFailsTransiently()
    {
        var commitFailures = new CommitFailureInterceptor();
        await using var testContext = await TestDbContextFactory.CreateRetryingSqliteInMemoryContextAsync(commitFailures);
        var dbContext = testContext.DbContext;
        await SeedPetWithRelatedDataAsync(dbContext);
        commitFailures.FailNextCommitWith(new TransientTestException("Simulated transient commit failure."));

        var storage = new FakeImageStorageService();
        var logger = new TestLogger<PetDeletionService>();
        var service = new PetDeletionService(dbContext, storage, logger);
        var pet = await dbContext.Pets.SingleAsync(x => x.Id == 1);

        await service.DeleteAsync(pet, "user-a");

        Assert.Equal(1, commitFailures.FailedCommitCount);
        await AssertOnlyOtherPetDataRemainsAsync(dbContext, expectedUsedImageBytes: 320);
        Assert.Equal(
            ["images/log-1.jpg", "images/pet-1.jpg", "images/visit-1.jpg"],
            storage.DeletedStorageKeys.Order().ToArray());
        Assert.Single(logger.Entries, entry => entry.Message.StartsWith("Completed deletion operation.", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, entry => entry.LogLevel == LogLevel.Error);
    }

    [Fact]
    public async Task DeleteAsync_RollsBackAndKeepsFiles_WhenCommitFails()
    {
        var commitFailures = new CommitFailureInterceptor();
        await using var testContext = await TestDbContextFactory.CreateRetryingSqliteInMemoryContextAsync(commitFailures);
        var dbContext = testContext.DbContext;
        await SeedPetWithRelatedDataAsync(dbContext);
        var failure = new InvalidOperationException("Simulated commit failure.");
        commitFailures.FailNextCommitWith(failure);

        var storage = new FakeImageStorageService();
        var logger = new TestLogger<PetDeletionService>();
        var service = new PetDeletionService(dbContext, storage, logger);
        var pet = await dbContext.Pets.SingleAsync(x => x.Id == 1);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(pet, "user-a"));

        Assert.Same(failure, thrown);
        dbContext.ChangeTracker.Clear();
        Assert.Equal(2, await dbContext.Pets.CountAsync());
        Assert.Equal(1, await dbContext.HealthLogs.CountAsync());
        Assert.Equal(1, await dbContext.HealthLogImages.CountAsync());
        Assert.Equal(1, await dbContext.Visits.CountAsync());
        Assert.Equal(1, await dbContext.VisitImages.CountAsync());
        Assert.Equal(2, await dbContext.ScheduleItems.CountAsync());
        Assert.Equal(4, await dbContext.ImageAssets.CountAsync());
        Assert.Equal(790, await dbContext.Users.Where(x => x.Id == "user-a").Select(x => x.UsedImageBytes).SingleAsync());
        Assert.Empty(storage.DeletedStorageKeys);
        Assert.Contains(
            logger.Entries,
            entry => entry.LogLevel == LogLevel.Error
                     && ReferenceEquals(entry.Exception, failure)
                     && Equals(entry.Properties["Operation"], ApplicationOperationLogging.Operations.DeletePet));
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.StartsWith("Completed deletion operation.", StringComparison.Ordinal));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"pet-deletion-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    // user-a（使用量 790 bytes）のペット1（写真 200 + 健康ログ画像 120 + 通院画像 150）と、残すべきペット2・アバター 320 を作る
    private static async Task SeedPetWithRelatedDataAsync(ApplicationDbContext dbContext)
    {
        var now = DateTimeOffset.UtcNow;
        var petPhotoImageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var healthLogImageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var visitImageId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var avatarImageId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        dbContext.ImageAssets.AddRange(
            NewImageAsset(petPhotoImageId, "user-a", "PetPhoto", "images/pet-1.jpg", 200),
            NewImageAsset(healthLogImageId, "user-a", "HealthLog", "images/log-1.jpg", 120),
            NewImageAsset(visitImageId, "user-a", "Visit", "images/visit-1.jpg", 150),
            NewImageAsset(avatarImageId, "user-a", "Avatar", "images/avatar-1.jpg", 320));

        dbContext.Users.Add(new ApplicationUser
        {
            Id = "user-a",
            UserName = "userA",
            AvatarImageId = avatarImageId,
            UsedImageBytes = 790
        });

        dbContext.Pets.AddRange(
            NewPet(1, "user-a", "Mugi", petPhotoImageId),
            NewPet(2, "user-a", "Sora", null));

        dbContext.HealthLogs.Add(new HealthLog { Id = 10, PetId = 1, RecordedAt = now, CreatedAt = now, UpdatedAt = now });
        dbContext.Visits.Add(new Visit { Id = 20, PetId = 1, VisitDate = new DateTime(2026, 3, 22), CreatedAt = now, UpdatedAt = now });
        dbContext.ScheduleItems.AddRange(
            new ScheduleItem
            {
                Id = 30,
                PetId = 1,
                DueDate = new DateTime(2026, 3, 30),
                Type = ScheduleItemTypeCatalog.Vaccine,
                Title = "Rabies",
                CreatedAt = now,
                UpdatedAt = now
            },
            new ScheduleItem
            {
                Id = 31,
                PetId = 2,
                DueDate = new DateTime(2026, 4, 1),
                Type = ScheduleItemTypeCatalog.Other,
                Title = "Reminder",
                CreatedAt = now,
                UpdatedAt = now
            });

        dbContext.HealthLogImages.Add(new HealthLogImage { Id = 1, HealthLogId = 10, ImageId = healthLogImageId, SortOrder = 1 });
        dbContext.VisitImages.Add(new VisitImage { Id = 1, VisitId = 20, ImageId = visitImageId, SortOrder = 1 });

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }

    private static async Task AssertOnlyOtherPetDataRemainsAsync(ApplicationDbContext dbContext, long expectedUsedImageBytes)
    {
        dbContext.ChangeTracker.Clear();

        Assert.Equal(2, Assert.Single(await dbContext.Pets.Select(x => x.Id).ToArrayAsync()));
        Assert.Equal(0, await dbContext.HealthLogs.CountAsync());
        Assert.Equal(0, await dbContext.HealthLogImages.CountAsync());
        Assert.Equal(0, await dbContext.Visits.CountAsync());
        Assert.Equal(0, await dbContext.VisitImages.CountAsync());
        Assert.Equal(31, Assert.Single(await dbContext.ScheduleItems.Select(x => x.Id).ToArrayAsync()));
        Assert.Equal("images/avatar-1.jpg", Assert.Single(await dbContext.ImageAssets.Select(x => x.StorageKey).ToArrayAsync()));
        Assert.Equal(
            expectedUsedImageBytes,
            await dbContext.Users.Where(x => x.Id == "user-a").Select(x => x.UsedImageBytes).SingleAsync());
    }

    private static Pet NewPet(int id, string ownerId, string name, Guid? photoImageId)
    {
        var now = DateTimeOffset.UtcNow;
        return new Pet
        {
            Id = id,
            OwnerId = ownerId,
            Name = name,
            SpeciesCode = "DOG",
            PhotoImageId = photoImageId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static ImageAsset NewImageAsset(Guid imageId, string ownerId, string category, string storageKey, long sizeBytes)
    {
        return new ImageAsset
        {
            ImageId = imageId,
            StorageKey = storageKey,
            ContentType = "image/jpeg",
            SizeBytes = sizeBytes,
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
            _ = extension;
            throw new NotSupportedException();
        }

        public Task SaveFormFileToPathAsync(IFormFile file, string destinationPath, CancellationToken cancellationToken = default)
        {
            _ = file;
            _ = destinationPath;
            _ = cancellationToken;
            throw new NotSupportedException();
        }

        public Task MoveToStorageAsync(string sourcePath, string storageKey, CancellationToken cancellationToken = default)
        {
            _ = sourcePath;
            _ = storageKey;
            _ = cancellationToken;
            throw new NotSupportedException();
        }

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            _ = storageKey;
            _ = cancellationToken;
            throw new NotSupportedException();
        }

        public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            DeletedStorageKeys.Add(storageKey);

            if (FailingStorageKeys.Contains(storageKey))
            {
                throw new IOException("Simulated delete failure.");
            }

            return Task.CompletedTask;
        }
    }
}

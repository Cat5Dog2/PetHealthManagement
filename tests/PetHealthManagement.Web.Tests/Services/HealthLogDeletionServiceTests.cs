using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Tests.Services;

public class HealthLogDeletionServiceTests
{
    [Fact]
    public async Task DeleteAsync_RemovesHealthLogAndImages_AndUpdatesUsedBytes()
    {
        await using var dbContext = CreateDbContext();
        var storage = new FakeImageStorageService();

        dbContext.Users.Add(new ApplicationUser
        {
            Id = "user-a",
            UserName = "userA",
            UsedImageBytes = 470
        });

        dbContext.Pets.Add(NewPet(1, "user-a"));
        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var firstImageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var secondImageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var avatarImageId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        dbContext.ImageAssets.AddRange(
            NewImageAsset(firstImageId, "user-a", "HealthLog", "images/log-1.jpg", 120),
            NewImageAsset(secondImageId, "user-a", "HealthLog", "images/log-2.jpg", 150),
            NewImageAsset(avatarImageId, "user-a", "Avatar", "images/avatar.jpg", 200));

        dbContext.HealthLogImages.AddRange(
            new HealthLogImage { Id = 1, HealthLogId = 10, ImageId = firstImageId, SortOrder = 1 },
            new HealthLogImage { Id = 2, HealthLogId = 10, ImageId = secondImageId, SortOrder = 2 });

        await dbContext.SaveChangesAsync();

        var service = new HealthLogDeletionService(dbContext, storage, NullLogger<HealthLogDeletionService>.Instance);
        var healthLog = await dbContext.HealthLogs.SingleAsync(x => x.Id == 10);

        await service.DeleteAsync(healthLog, "user-a");

        var owner = await dbContext.Users.SingleAsync(x => x.Id == "user-a");

        Assert.Equal(0, await dbContext.HealthLogs.CountAsync());
        Assert.Equal(0, await dbContext.HealthLogImages.CountAsync());
        Assert.Single(await dbContext.ImageAssets.ToListAsync());
        Assert.Equal(200, owner.UsedImageBytes);
        Assert.Equal(
            ["images/log-1.jpg", "images/log-2.jpg"],
            storage.DeletedStorageKeys.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task DeleteAsync_Continues_WhenFileDeletionFails()
    {
        await using var dbContext = CreateDbContext();
        var storage = new FakeImageStorageService
        {
            FailingStorageKeys = ["images/log-1.jpg"]
        };

        dbContext.Users.Add(new ApplicationUser
        {
            Id = "user-a",
            UserName = "userA",
            UsedImageBytes = 120
        });

        dbContext.Pets.Add(NewPet(1, "user-a"));
        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var imageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        dbContext.ImageAssets.Add(NewImageAsset(imageId, "user-a", "HealthLog", "images/log-1.jpg", 120));
        dbContext.HealthLogImages.Add(new HealthLogImage
        {
            Id = 1,
            HealthLogId = 10,
            ImageId = imageId,
            SortOrder = 1
        });

        await dbContext.SaveChangesAsync();

        var service = new HealthLogDeletionService(dbContext, storage, NullLogger<HealthLogDeletionService>.Instance);
        var healthLog = await dbContext.HealthLogs.SingleAsync(x => x.Id == 10);

        await service.DeleteAsync(healthLog, "user-a");

        Assert.Empty(await dbContext.HealthLogs.ToListAsync());
        Assert.Empty(await dbContext.HealthLogImages.ToListAsync());
        Assert.Empty(await dbContext.ImageAssets.ToListAsync());
        Assert.Contains("images/log-1.jpg", storage.DeletedStorageKeys);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"healthlog-deletion-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Pet NewPet(int id, string ownerId)
    {
        var now = DateTimeOffset.UtcNow;
        return new Pet
        {
            Id = id,
            OwnerId = ownerId,
            Name = "Mugi",
            SpeciesCode = "DOG",
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

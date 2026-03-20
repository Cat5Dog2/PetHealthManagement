using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Tests.Services;

public class UserDataDeletionServiceTests
{
    [Fact]
    public async Task DeleteUserAsync_RemovesOwnedUserPetsAndImages()
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
            NewImageAsset(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "user-b", "Avatar", "images/avatar-b.jpg"));

        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = DateTimeOffset.UtcNow,
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

        await dbContext.SaveChangesAsync();

        var storage = new FakeImageStorageService();
        var service = new UserDataDeletionService(dbContext, storage, NullLogger<UserDataDeletionService>.Instance);

        var deleted = await service.DeleteUserAsync("user-a");

        Assert.True(deleted);
        Assert.Null(await dbContext.Users.SingleOrDefaultAsync(x => x.Id == "user-a"));
        Assert.Equal(1, await dbContext.Users.CountAsync());
        Assert.Equal(1, await dbContext.Pets.CountAsync());
        Assert.Equal(0, await dbContext.HealthLogs.CountAsync());
        Assert.Equal(0, await dbContext.HealthLogImages.CountAsync());
        Assert.Equal(1, await dbContext.ImageAssets.CountAsync());
        Assert.Equal(
            ["images/avatar-a.jpg", "images/log-a.jpg", "images/pet-a.jpg"],
            storage.DeletedStorageKeys.OrderBy(x => x).ToArray());
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

        var service = new UserDataDeletionService(dbContext, storage, NullLogger<UserDataDeletionService>.Instance);

        var deleted = await service.DeleteUserAsync("user-a");

        Assert.True(deleted);
        Assert.Empty(await dbContext.Users.ToListAsync());
        Assert.Empty(await dbContext.ImageAssets.ToListAsync());
    }

    [Fact]
    public async Task DeleteUserAsync_ReturnsFalse_WhenUserDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var service = new UserDataDeletionService(dbContext, new FakeImageStorageService(), NullLogger<UserDataDeletionService>.Instance);

        var deleted = await service.DeleteUserAsync("missing-user");

        Assert.False(deleted);
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

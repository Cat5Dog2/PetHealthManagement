using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PetHealthManagement.Web.Tests.Services;

public class HealthLogImageServiceTests
{
    [Fact]
    public async Task ApplyImageChangesAsync_AddsNewImages_DeletesSelectedImages_AndUpdatesUsedBytes()
    {
        await using var dbContext = CreateDbContext();
        using var storage = new FakeImageStorageService();

        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(1, "user-a"));
        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var deleteImageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var keepImageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var avatarImageId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        dbContext.ImageAssets.AddRange(
            NewImageAsset(deleteImageId, "user-a", "HealthLog", "images/delete.jpg", 120),
            NewImageAsset(keepImageId, "user-a", "HealthLog", "images/keep.jpg", 150),
            NewImageAsset(avatarImageId, "user-a", "Avatar", "images/avatar.jpg", 80));

        dbContext.HealthLogImages.AddRange(
            new HealthLogImage { Id = 1, HealthLogId = 10, ImageId = deleteImageId, SortOrder = 1 },
            new HealthLogImage { Id = 2, HealthLogId = 10, ImageId = keepImageId, SortOrder = 2 });

        await dbContext.SaveChangesAsync();

        var service = new HealthLogImageService(dbContext, storage, NullLogger<HealthLogImageService>.Instance);
        var healthLog = await dbContext.HealthLogs.SingleAsync(x => x.Id == 10);

        var result = await service.ApplyImageChangesAsync(
            healthLog,
            "user-a",
            [CreateImageFormFile("new.png", "image/png")],
            [deleteImageId]);

        var owner = await dbContext.Users.SingleAsync(x => x.Id == "user-a");
        var assets = await dbContext.ImageAssets
            .Where(x => x.OwnerId == "user-a")
            .OrderBy(x => x.StorageKey)
            .ToListAsync();
        var logImages = await dbContext.HealthLogImages
            .Where(x => x.HealthLogId == 10)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(assets, x => x.ImageId == deleteImageId);
        Assert.Equal(3, assets.Count);
        Assert.Equal(2, logImages.Count);
        Assert.Equal(keepImageId, logImages[0].ImageId);
        Assert.Equal(assets.Where(x => x.Status == ImageAssetStatus.Ready).Sum(x => x.SizeBytes), owner.UsedImageBytes);
        Assert.Contains("images/delete.jpg", storage.DeletedStorageKeys);
        Assert.Single(storage.MovedStorageKeys);
    }

    [Fact]
    public async Task ApplyImageChangesAsync_Fails_WhenImageCountWouldExceedLimit()
    {
        await using var dbContext = CreateDbContext();
        using var storage = new FakeImageStorageService();

        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(1, "user-a"));
        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        for (var index = 1; index <= 10; index++)
        {
            var imageId = Guid.NewGuid();
            dbContext.ImageAssets.Add(NewImageAsset(imageId, "user-a", "HealthLog", $"images/{index}.jpg", 100));
            dbContext.HealthLogImages.Add(new HealthLogImage
            {
                Id = index,
                HealthLogId = 10,
                ImageId = imageId,
                SortOrder = index
            });
        }

        await dbContext.SaveChangesAsync();

        var service = new HealthLogImageService(dbContext, storage, NullLogger<HealthLogImageService>.Instance);
        var healthLog = await dbContext.HealthLogs.SingleAsync(x => x.Id == 10);

        var result = await service.ApplyImageChangesAsync(
            healthLog,
            "user-a",
            [CreateImageFormFile("overflow.png", "image/png")],
            []);

        Assert.False(result.Succeeded);
        Assert.Equal(10, await dbContext.HealthLogImages.CountAsync(x => x.HealthLogId == 10));
        Assert.Empty(storage.MovedStorageKeys);
    }

    [Fact]
    public async Task ApplyImageChangesAsync_Succeeds_WhenOldFileDeletionFails()
    {
        await using var dbContext = CreateDbContext();
        using var storage = new FakeImageStorageService
        {
            FailingDeleteStorageKeys = ["images/delete.jpg"]
        };

        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
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
        dbContext.ImageAssets.Add(NewImageAsset(imageId, "user-a", "HealthLog", "images/delete.jpg", 120));
        dbContext.HealthLogImages.Add(new HealthLogImage
        {
            Id = 1,
            HealthLogId = 10,
            ImageId = imageId,
            SortOrder = 1
        });

        await dbContext.SaveChangesAsync();

        var service = new HealthLogImageService(dbContext, storage, NullLogger<HealthLogImageService>.Instance);
        var healthLog = await dbContext.HealthLogs.SingleAsync(x => x.Id == 10);

        var result = await service.ApplyImageChangesAsync(healthLog, "user-a", [], [imageId]);

        Assert.True(result.Succeeded);
        Assert.Empty(await dbContext.HealthLogImages.ToListAsync());
        Assert.Empty(await dbContext.ImageAssets.ToListAsync());
        Assert.Contains("images/delete.jpg", storage.DeletedStorageKeys);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"healthlog-image-tests-{Guid.NewGuid()}")
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

    private static IFormFile CreateImageFormFile(string fileName, string contentType)
    {
        using var image = new Image<Rgba32>(16, 16, Color.CadetBlue);
        var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;

        return new FormFile(stream, 0, stream.Length, "NewFiles", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class FakeImageStorageService : IImageStorageService, IDisposable
    {
        private readonly string rootPath = Path.Combine(Path.GetTempPath(), "healthlog-image-tests", Guid.NewGuid().ToString("N"));

        public HashSet<string> FailingDeleteStorageKeys { get; init; } = [];

        public List<string> DeletedStorageKeys { get; } = [];

        public List<string> MovedStorageKeys { get; } = [];

        public FakeImageStorageService()
        {
            Directory.CreateDirectory(rootPath);
        }

        public string CreateTemporaryPath(string extension)
        {
            return Path.Combine(rootPath, $"{Guid.NewGuid():N}{extension}");
        }

        public async Task SaveFormFileToPathAsync(IFormFile file, string destinationPath, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            await using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(destination, cancellationToken);
        }

        public Task MoveToStorageAsync(string sourcePath, string storageKey, CancellationToken cancellationToken = default)
        {
            MovedStorageKeys.Add(storageKey);
            var destinationPath = Path.Combine(rootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
            File.Delete(sourcePath);
            return Task.CompletedTask;
        }

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            DeletedStorageKeys.Add(storageKey);

            if (FailingDeleteStorageKeys.Contains(storageKey))
            {
                throw new IOException("Simulated delete failure.");
            }

            var path = Path.Combine(rootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(rootPath))
                {
                    Directory.Delete(rootPath, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}

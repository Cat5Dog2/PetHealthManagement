using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.Tests.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PetHealthManagement.Web.Tests.Services;

public class VisitImageServiceTests
{
    [Fact]
    public async Task ApplyImageChangesAsync_AddsNewImages_DeletesSelectedImages_AndUpdatesUsedBytes()
    {
        await using var dbContext = CreateDbContext();
        using var storage = new FakeImageStorageService();

        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(1, "user-a"));
        dbContext.Visits.Add(new Visit
        {
            Id = 10,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var deleteImageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var keepImageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var avatarImageId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        dbContext.ImageAssets.AddRange(
            NewImageAsset(deleteImageId, "user-a", "Visit", "images/delete.jpg", 120),
            NewImageAsset(keepImageId, "user-a", "Visit", "images/keep.jpg", 150),
            NewImageAsset(avatarImageId, "user-a", "Avatar", "images/avatar.jpg", 80));

        dbContext.VisitImages.AddRange(
            new VisitImage { Id = 1, VisitId = 10, ImageId = deleteImageId, SortOrder = 1 },
            new VisitImage { Id = 2, VisitId = 10, ImageId = keepImageId, SortOrder = 2 });

        await dbContext.SaveChangesAsync();

        var service = new VisitImageService(dbContext, storage, NullLogger<VisitImageService>.Instance);
        var visit = await dbContext.Visits.SingleAsync(x => x.Id == 10);

        var result = await service.ApplyImageChangesAsync(
            visit,
            "user-a",
            [CreateImageFormFile("new.png", "image/png")],
            [deleteImageId]);

        var owner = await dbContext.Users.SingleAsync(x => x.Id == "user-a");
        var assets = await dbContext.ImageAssets
            .Where(x => x.OwnerId == "user-a")
            .OrderBy(x => x.StorageKey)
            .ToListAsync();
        var visitImages = await dbContext.VisitImages
            .Where(x => x.VisitId == 10)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(assets, x => x.ImageId == deleteImageId);
        Assert.Equal(3, assets.Count);
        Assert.Equal(2, visitImages.Count);
        Assert.Equal(keepImageId, visitImages[0].ImageId);
        Assert.Equal(assets.Where(x => x.Status == ImageAssetStatus.Ready).Sum(x => x.SizeBytes), owner.UsedImageBytes);
        Assert.Contains("images/delete.jpg", storage.DeletedStorageKeys);
        Assert.Single(storage.MovedStorageKeys);
    }

    [Fact]
    public async Task ApplyImageChangesAsync_Fails_WhenImageCountWouldExceedLimit()
    {
        await using var dbContext = CreateDbContext();
        using var storage = new FakeImageStorageService();
        var logger = new TestLogger<VisitImageService>();

        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(1, "user-a"));
        dbContext.Visits.Add(new Visit
        {
            Id = 10,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        for (var index = 1; index <= 10; index++)
        {
            var imageId = Guid.NewGuid();
            dbContext.ImageAssets.Add(NewImageAsset(imageId, "user-a", "Visit", $"images/{index}.jpg", 100));
            dbContext.VisitImages.Add(new VisitImage
            {
                Id = index,
                VisitId = 10,
                ImageId = imageId,
                SortOrder = index
            });
        }

        await dbContext.SaveChangesAsync();

        var service = new VisitImageService(dbContext, storage, logger);
        var visit = await dbContext.Visits.SingleAsync(x => x.Id == 10);

        var result = await service.ApplyImageChangesAsync(
            visit,
            "user-a",
            [CreateImageFormFile("overflow.png", "image/png")],
            []);

        Assert.False(result.Succeeded);
        Assert.Equal(ImageUploadErrorMessages.TooManyAttachments, result.ErrorMessage);
        Assert.Equal(10, await dbContext.VisitImages.CountAsync(x => x.VisitId == 10));
        Assert.Empty(storage.MovedStorageKeys);
        var rejectionLog = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, rejectionLog.LogLevel);
        Assert.Equal(ImageOperationLogging.Reasons.AttachmentLimitExceeded, rejectionLog.Properties["Reason"]);
        Assert.Equal("Visit", rejectionLog.Properties["ImageCategory"]);
        Assert.Equal("Visit", rejectionLog.Properties["ResourceType"]);
        Assert.Equal(10, rejectionLog.Properties["ResourceId"]);
        Assert.Equal(10, rejectionLog.Properties["ExistingImageCount"]);
        Assert.Equal(1, rejectionLog.Properties["RequestedNewFileCount"]);
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
        dbContext.Visits.Add(new Visit
        {
            Id = 10,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var imageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        dbContext.ImageAssets.Add(NewImageAsset(imageId, "user-a", "Visit", "images/delete.jpg", 120));
        dbContext.VisitImages.Add(new VisitImage
        {
            Id = 1,
            VisitId = 10,
            ImageId = imageId,
            SortOrder = 1
        });

        await dbContext.SaveChangesAsync();

        var service = new VisitImageService(dbContext, storage, NullLogger<VisitImageService>.Instance);
        var visit = await dbContext.Visits.SingleAsync(x => x.Id == 10);

        var result = await service.ApplyImageChangesAsync(visit, "user-a", [], [imageId]);

        Assert.True(result.Succeeded);
        Assert.Empty(await dbContext.VisitImages.ToListAsync());
        Assert.Empty(await dbContext.ImageAssets.ToListAsync());
        Assert.Contains("images/delete.jpg", storage.DeletedStorageKeys);
    }

    [Fact]
    public async Task ApplyImageChangesAsync_FailsWithWarningLog_WhenUploadedFileIsNotAnImage()
    {
        await using var dbContext = CreateDbContext();
        using var storage = new FakeImageStorageService();
        var logger = new TestLogger<VisitImageService>();

        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(1, "user-a"));
        dbContext.Visits.Add(new Visit
        {
            Id = 10,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var service = new VisitImageService(dbContext, storage, logger);
        var visit = await dbContext.Visits.SingleAsync(x => x.Id == 10);

        var result = await service.ApplyImageChangesAsync(
            visit,
            "user-a",
            [CreateTextFormFile("fake.png", "image/png")],
            []);

        Assert.False(result.Succeeded);
        Assert.Equal(ImageUploadErrorMessages.UnsupportedFormat, result.ErrorMessage);
        Assert.Empty(storage.MovedStorageKeys);
        var rejectionLog = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, rejectionLog.LogLevel);
        Assert.Equal(ImageOperationLogging.Reasons.UnsupportedImageData, rejectionLog.Properties["Reason"]);
        Assert.Equal("Visit", rejectionLog.Properties["ImageCategory"]);
        Assert.Equal("user-a", rejectionLog.Properties["OwnerId"]);
        Assert.Equal("Visit", rejectionLog.Properties["ResourceType"]);
        Assert.Equal(10, rejectionLog.Properties["ResourceId"]);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"visit-image-tests-{Guid.NewGuid()}")
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

    private static IFormFile CreateTextFormFile(string fileName, string contentType)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("this is not a real image");
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, stream.Length, "NewFiles", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class FakeImageStorageService : IImageStorageService, IDisposable
    {
        private readonly string rootPath = Path.Combine(Path.GetTempPath(), "visit-image-tests", Guid.NewGuid().ToString("N"));

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
            _ = cancellationToken;

            MovedStorageKeys.Add(storageKey);
            var destinationPath = Path.Combine(rootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
            File.Delete(sourcePath);
            return Task.CompletedTask;
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

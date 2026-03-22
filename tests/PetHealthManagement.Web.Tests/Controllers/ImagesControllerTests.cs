using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Controllers;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Tests.Controllers;

public class ImagesControllerTests
{
    [Fact]
    public async Task Get_ReturnsNotFound_WhenImageAssetDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var storage = new FakeImageStorageService();
        var controller = BuildController(dbContext, storage, "user-a");

        var result = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenImageAssetIsPending()
    {
        await using var dbContext = CreateDbContext();
        var imageId = Guid.NewGuid();

        dbContext.ImageAssets.Add(new ImageAsset
        {
            ImageId = imageId,
            StorageKey = "images/test.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            OwnerId = "user-a",
            Category = "PetPhoto",
            Status = ImageAssetStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var storage = new FakeImageStorageService();
        storage.Add("images/test.jpg", [1, 2, 3]);

        var controller = BuildController(dbContext, storage, "user-a");
        var result = await controller.Get(imageId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenPetIsPrivate_AndRequesterIsNotOwner()
    {
        await using var dbContext = CreateDbContext();
        var imageId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new ApplicationUser { Id = "user-a", UserName = "owner" },
            new ApplicationUser { Id = "user-b", UserName = "other" });

        dbContext.ImageAssets.Add(new ImageAsset
        {
            ImageId = imageId,
            StorageKey = "images/private.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            OwnerId = "user-a",
            Category = "PetPhoto",
            Status = ImageAssetStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow
        });

        dbContext.Pets.Add(new Pet
        {
            Id = 1,
            OwnerId = "user-a",
            Name = "Private Pet",
            SpeciesCode = "DOG",
            IsPublic = false,
            PhotoImageId = imageId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var storage = new FakeImageStorageService();
        storage.Add("images/private.jpg", [1, 2, 3]);

        var controller = BuildController(dbContext, storage, "user-b");
        var result = await controller.Get(imageId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_ReturnsFile_WhenPetIsPublic()
    {
        await using var dbContext = CreateDbContext();
        var imageId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new ApplicationUser { Id = "user-a", UserName = "owner" },
            new ApplicationUser { Id = "user-b", UserName = "other" });

        dbContext.ImageAssets.Add(new ImageAsset
        {
            ImageId = imageId,
            StorageKey = "images/public.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            OwnerId = "user-a",
            Category = "PetPhoto",
            Status = ImageAssetStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow
        });

        dbContext.Pets.Add(new Pet
        {
            Id = 2,
            OwnerId = "user-a",
            Name = "Public Pet",
            SpeciesCode = "DOG",
            IsPublic = true,
            PhotoImageId = imageId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var storage = new FakeImageStorageService();
        storage.Add("images/public.jpg", [1, 2, 3, 4]);

        var controller = BuildController(dbContext, storage, "user-b");
        var result = await controller.Get(imageId, CancellationToken.None);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/jpeg", fileResult.ContentType);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal("inline", controller.Response.Headers.ContentDisposition.ToString());
        Assert.Equal("nosniff", controller.Response.Headers.XContentTypeOptions.ToString());
    }

    [Fact]
    public async Task Get_ReturnsFile_ForOwnerAvatar()
    {
        await using var dbContext = CreateDbContext();
        var imageId = Guid.NewGuid();

        dbContext.Users.Add(new ApplicationUser
        {
            Id = "user-a",
            UserName = "owner",
            AvatarImageId = imageId
        });

        dbContext.ImageAssets.Add(new ImageAsset
        {
            ImageId = imageId,
            StorageKey = "images/avatar.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            OwnerId = "user-a",
            Category = "Avatar",
            Status = ImageAssetStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var storage = new FakeImageStorageService();
        storage.Add("images/avatar.jpg", [1, 2, 3]);

        var controller = BuildController(dbContext, storage, "user-a");
        var result = await controller.Get(imageId, CancellationToken.None);

        Assert.IsType<FileStreamResult>(result);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_ForOtherUsersAvatar()
    {
        await using var dbContext = CreateDbContext();
        var imageId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new ApplicationUser
            {
                Id = "user-a",
                UserName = "owner",
                AvatarImageId = imageId
            },
            new ApplicationUser
            {
                Id = "user-b",
                UserName = "other"
            });

        dbContext.ImageAssets.Add(new ImageAsset
        {
            ImageId = imageId,
            StorageKey = "images/avatar.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            OwnerId = "user-a",
            Category = "Avatar",
            Status = ImageAssetStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var storage = new FakeImageStorageService();
        storage.Add("images/avatar.jpg", [1, 2, 3]);

        var controller = BuildController(dbContext, storage, "user-b");
        var result = await controller.Get(imageId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_ReturnsFile_ForOwnerHealthLogImage()
    {
        await using var dbContext = CreateDbContext();
        var imageId = Guid.NewGuid();

        dbContext.ImageAssets.Add(new ImageAsset
        {
            ImageId = imageId,
            StorageKey = "images/healthlog.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            OwnerId = "user-a",
            Category = "HealthLog",
            Status = ImageAssetStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow
        });

        dbContext.Pets.Add(new Pet
        {
            Id = 10,
            OwnerId = "user-a",
            Name = "Mugi",
            SpeciesCode = "DOG",
            IsPublic = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 20,
            PetId = 10,
            RecordedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.HealthLogImages.Add(new HealthLogImage
        {
            Id = 1,
            HealthLogId = 20,
            ImageId = imageId,
            SortOrder = 1
        });

        await dbContext.SaveChangesAsync();

        var storage = new FakeImageStorageService();
        storage.Add("images/healthlog.jpg", [1, 2, 3]);

        var controller = BuildController(dbContext, storage, "user-a");
        var result = await controller.Get(imageId, CancellationToken.None);

        Assert.IsType<FileStreamResult>(result);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_ForOtherUsersHealthLogImage()
    {
        await using var dbContext = CreateDbContext();
        var imageId = Guid.NewGuid();

        dbContext.ImageAssets.Add(new ImageAsset
        {
            ImageId = imageId,
            StorageKey = "images/healthlog.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            OwnerId = "user-a",
            Category = "HealthLog",
            Status = ImageAssetStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow
        });

        dbContext.Pets.Add(new Pet
        {
            Id = 10,
            OwnerId = "user-a",
            Name = "Mugi",
            SpeciesCode = "DOG",
            IsPublic = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 20,
            PetId = 10,
            RecordedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.HealthLogImages.Add(new HealthLogImage
        {
            Id = 1,
            HealthLogId = 20,
            ImageId = imageId,
            SortOrder = 1
        });

        await dbContext.SaveChangesAsync();

        var storage = new FakeImageStorageService();
        storage.Add("images/healthlog.jpg", [1, 2, 3]);

        var controller = BuildController(dbContext, storage, "user-b");
        var result = await controller.Get(imageId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_ReturnsFile_ForOwnerVisitImage()
    {
        await using var dbContext = CreateDbContext();
        var imageId = Guid.NewGuid();

        dbContext.ImageAssets.Add(new ImageAsset
        {
            ImageId = imageId,
            StorageKey = "images/visit.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            OwnerId = "user-a",
            Category = "Visit",
            Status = ImageAssetStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow
        });

        dbContext.Pets.Add(new Pet
        {
            Id = 10,
            OwnerId = "user-a",
            Name = "Mugi",
            SpeciesCode = "DOG",
            IsPublic = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.Visits.Add(new Visit
        {
            Id = 30,
            PetId = 10,
            VisitDate = new DateTime(2026, 3, 21),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.VisitImages.Add(new VisitImage
        {
            Id = 1,
            VisitId = 30,
            ImageId = imageId,
            SortOrder = 1
        });

        await dbContext.SaveChangesAsync();

        var storage = new FakeImageStorageService();
        storage.Add("images/visit.jpg", [1, 2, 3]);

        var controller = BuildController(dbContext, storage, "user-a");
        var result = await controller.Get(imageId, CancellationToken.None);

        Assert.IsType<FileStreamResult>(result);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_ForOtherUsersVisitImage()
    {
        await using var dbContext = CreateDbContext();
        var imageId = Guid.NewGuid();

        dbContext.ImageAssets.Add(new ImageAsset
        {
            ImageId = imageId,
            StorageKey = "images/visit.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            OwnerId = "user-a",
            Category = "Visit",
            Status = ImageAssetStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow
        });

        dbContext.Pets.Add(new Pet
        {
            Id = 10,
            OwnerId = "user-a",
            Name = "Mugi",
            SpeciesCode = "DOG",
            IsPublic = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.Visits.Add(new Visit
        {
            Id = 30,
            PetId = 10,
            VisitDate = new DateTime(2026, 3, 21),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.VisitImages.Add(new VisitImage
        {
            Id = 1,
            VisitId = 30,
            ImageId = imageId,
            SortOrder = 1
        });

        await dbContext.SaveChangesAsync();

        var storage = new FakeImageStorageService();
        storage.Add("images/visit.jpg", [1, 2, 3]);

        var controller = BuildController(dbContext, storage, "user-b");
        var result = await controller.Get(imageId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static ImagesController BuildController(ApplicationDbContext dbContext, IImageStorageService storage, string userId)
    {
        var controller = new ImagesController(dbContext, storage);
        var claimsPrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };

        return controller;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"images-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FakeImageStorageService : IImageStorageService
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public void Add(string storageKey, byte[] bytes)
        {
            _files[storageKey] = bytes;
        }

        public string CreateTemporaryPath(string extension) => $"tmp-{Guid.NewGuid():N}{extension}";

        public Task SaveFormFileToPathAsync(IFormFile file, string destinationPath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MoveToStorageAsync(string sourcePath, string storageKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            if (!_files.TryGetValue(storageKey, out var bytes))
            {
                return Task.FromResult<Stream?>(null);
            }

            Stream stream = new MemoryStream(bytes);
            return Task.FromResult<Stream?>(stream);
        }

        public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            _files.Remove(storageKey);
            return Task.CompletedTask;
        }
    }
}

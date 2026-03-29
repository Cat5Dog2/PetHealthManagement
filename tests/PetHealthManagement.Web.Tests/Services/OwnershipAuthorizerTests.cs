using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;

namespace PetHealthManagement.Web.Tests.Services;

public class OwnershipAuthorizerTests
{
    [Fact]
    public async Task FindOwnedPetAsync_ReturnsPetOnlyForOwner()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a"));
        await dbContext.SaveChangesAsync();

        var authorizer = new OwnershipAuthorizer(dbContext);

        var ownedPet = await authorizer.FindOwnedPetAsync(1, "user-a");
        var hiddenPet = await authorizer.FindOwnedPetAsync(1, "user-b");

        Assert.NotNull(ownedPet);
        Assert.Null(hiddenPet);
    }

    [Fact]
    public async Task FindOwnedHealthLogAsync_ReturnsHealthLogOnlyForOwner()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a"));
        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var authorizer = new OwnershipAuthorizer(dbContext);

        var ownedHealthLog = await authorizer.FindOwnedHealthLogAsync(10, "user-a");
        var hiddenHealthLog = await authorizer.FindOwnedHealthLogAsync(10, "user-b");

        Assert.NotNull(ownedHealthLog);
        Assert.Equal(1, ownedHealthLog.PetId);
        Assert.Equal("user-a", ownedHealthLog.Pet.OwnerId);
        Assert.Null(hiddenHealthLog);
    }

    [Fact]
    public async Task FindOwnedVisitAsync_ReturnsVisitOnlyForOwner()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a"));
        dbContext.Visits.Add(new Visit
        {
            Id = 20,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 29),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var authorizer = new OwnershipAuthorizer(dbContext);

        var ownedVisit = await authorizer.FindOwnedVisitAsync(20, "user-a");
        var hiddenVisit = await authorizer.FindOwnedVisitAsync(20, "user-b");

        Assert.NotNull(ownedVisit);
        Assert.Equal(1, ownedVisit.PetId);
        Assert.Equal("user-a", ownedVisit.Pet.OwnerId);
        Assert.Null(hiddenVisit);
    }

    [Fact]
    public async Task FindOwnedScheduleItemAsync_ReturnsScheduleItemOnlyForOwner()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a"));
        dbContext.ScheduleItems.Add(new ScheduleItem
        {
            Id = 30,
            PetId = 1,
            DueDate = new DateTime(2026, 3, 29),
            Type = ScheduleItemTypeCatalog.Other,
            Title = "Reminder",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var authorizer = new OwnershipAuthorizer(dbContext);

        var ownedScheduleItem = await authorizer.FindOwnedScheduleItemAsync(30, "user-a");
        var hiddenScheduleItem = await authorizer.FindOwnedScheduleItemAsync(30, "user-b");

        Assert.NotNull(ownedScheduleItem);
        Assert.Equal("user-a", ownedScheduleItem.Pet.OwnerId);
        Assert.Null(hiddenScheduleItem);
    }

    [Fact]
    public async Task FindReadableImageAssetAsync_AllowsOwnerOnlyCategoriesAndPublicPetPhotos()
    {
        await using var dbContext = CreateDbContext();
        var avatarImageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var publicPetPhotoId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var healthLogImageId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var visitImageId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        dbContext.Users.AddRange(
            new ApplicationUser { Id = "user-a", UserName = "owner", AvatarImageId = avatarImageId },
            new ApplicationUser { Id = "user-b", UserName = "other" });

        dbContext.Pets.AddRange(
            NewPet(1, "user-a", isPublic: true, photoImageId: publicPetPhotoId),
            NewPet(2, "user-a"));

        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 2,
            RecordedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.Visits.Add(new Visit
        {
            Id = 20,
            PetId = 2,
            VisitDate = new DateTime(2026, 3, 29),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

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

        dbContext.ImageAssets.AddRange(
            NewImageAsset(avatarImageId, "user-a", "Avatar"),
            NewImageAsset(publicPetPhotoId, "user-a", "PetPhoto"),
            NewImageAsset(healthLogImageId, "user-a", "HealthLog"),
            NewImageAsset(visitImageId, "user-a", "Visit"));

        await dbContext.SaveChangesAsync();

        var authorizer = new OwnershipAuthorizer(dbContext);

        Assert.NotNull(await authorizer.FindReadableImageAssetAsync(avatarImageId, "user-a"));
        Assert.Null(await authorizer.FindReadableImageAssetAsync(avatarImageId, "user-b"));
        Assert.NotNull(await authorizer.FindReadableImageAssetAsync(publicPetPhotoId, "user-b"));
        Assert.NotNull(await authorizer.FindReadableImageAssetAsync(healthLogImageId, "user-a"));
        Assert.Null(await authorizer.FindReadableImageAssetAsync(healthLogImageId, "user-b"));
        Assert.NotNull(await authorizer.FindReadableImageAssetAsync(visitImageId, "user-a"));
        Assert.Null(await authorizer.FindReadableImageAssetAsync(visitImageId, "user-b"));
    }

    [Fact]
    public async Task FindReadableImageAssetAsync_HidesPendingAndBrokenImages()
    {
        await using var dbContext = CreateDbContext();
        var pendingImageId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var brokenImageId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        dbContext.ImageAssets.AddRange(
            new ImageAsset
            {
                ImageId = pendingImageId,
                StorageKey = "images/pending.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 120,
                OwnerId = "user-a",
                Category = "PetPhoto",
                Status = ImageAssetStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            },
            NewImageAsset(brokenImageId, "user-a", "HealthLog"));

        await dbContext.SaveChangesAsync();

        var authorizer = new OwnershipAuthorizer(dbContext);

        Assert.Null(await authorizer.FindReadableImageAssetAsync(pendingImageId, "user-a"));
        Assert.Null(await authorizer.FindReadableImageAssetAsync(brokenImageId, "user-a"));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ownership-authorizer-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Pet NewPet(int id, string ownerId, bool isPublic = false, Guid? photoImageId = null)
    {
        return new Pet
        {
            Id = id,
            OwnerId = ownerId,
            Name = $"Pet {id}",
            SpeciesCode = "DOG",
            IsPublic = isPublic,
            PhotoImageId = photoImageId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static ImageAsset NewImageAsset(Guid imageId, string ownerId, string category)
    {
        return new ImageAsset
        {
            ImageId = imageId,
            StorageKey = $"images/{imageId:N}.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 128,
            OwnerId = ownerId,
            Category = category,
            Status = ImageAssetStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}

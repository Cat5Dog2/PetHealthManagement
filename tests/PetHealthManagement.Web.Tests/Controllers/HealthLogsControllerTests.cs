using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Controllers;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.ViewModels.HealthLogs;

namespace PetHealthManagement.Web.Tests.Controllers;

public class HealthLogsControllerTests
{
    [Fact]
    public async Task Index_ReturnsBadRequest_WhenPetIdIsMissing()
    {
        await using var dbContext = CreateDbContext();
        var controller = BuildController(dbContext, "user-a");

        var result = await controller.Index(petId: null, page: null);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Index_ReturnsNotFound_ForNonOwnerPet()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Index(petId: 1, page: null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Index_ReturnsPagedLogs_ForOwner_AndNormalizesPage()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));

        var baseTime = new DateTimeOffset(2026, 3, 21, 9, 0, 0, TimeSpan.FromHours(9));
        for (var index = 1; index <= 11; index++)
        {
            dbContext.HealthLogs.Add(new HealthLog
            {
                Id = index,
                PetId = 1,
                RecordedAt = baseTime.AddMinutes(index <= 2 ? 0 : -index),
                WeightKg = 5.0 + (index / 10.0),
                Note = $"Note {index}",
                CreatedAt = baseTime,
                UpdatedAt = baseTime
            });
        }

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Index(petId: 1, page: "abc");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HealthLogIndexViewModel>(viewResult.Model);

        Assert.Equal(1, model.Page);
        Assert.Equal(11, model.TotalCount);
        Assert.Equal(10, model.HealthLogs.Count);
        Assert.Equal(2, model.HealthLogs[0].HealthLogId);
        Assert.Equal(1, model.HealthLogs[1].HealthLogId);
    }

    [Fact]
    public async Task Details_ReturnsNotFound_ForNonOwnerHealthLog()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = new DateTimeOffset(2026, 3, 21, 9, 0, 0, TimeSpan.FromHours(9)),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Details(10, "/HealthLogs?petId=1");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ReturnsView_WithFallbackReturnUrl_AndImages()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));

        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = new DateTimeOffset(2026, 3, 21, 9, 0, 0, TimeSpan.FromHours(9)),
            WeightKg = 5.4,
            FoodAmountGram = 120,
            WalkMinutes = 45,
            StoolCondition = "良好",
            Note = "元気でした。",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var firstImageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var secondImageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        dbContext.ImageAssets.AddRange(
            NewImageAsset(firstImageId, "user-a", "images/log-1.jpg"),
            NewImageAsset(secondImageId, "user-a", "images/log-2.jpg"));

        dbContext.HealthLogImages.AddRange(
            new HealthLogImage { Id = 1, HealthLogId = 10, ImageId = firstImageId, SortOrder = 2 },
            new HealthLogImage { Id = 2, HealthLogId = 10, ImageId = secondImageId, SortOrder = 1 });

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Details(10, "https://evil.example/");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HealthLogDetailsViewModel>(viewResult.Model);

        Assert.Equal("Mugi", model.PetName);
        Assert.Equal("/HealthLogs?petId=1", model.ReturnUrl);
        Assert.Equal(2, model.Images.Count);
        Assert.Equal(secondImageId, model.Images[0].ImageId);
        Assert.Equal($"/images/{secondImageId:D}", model.Images[0].Url);
    }

    private static HealthLogsController BuildController(ApplicationDbContext dbContext, string userId)
    {
        var controller = new HealthLogsController(dbContext);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId)],
                    "TestAuth"))
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return controller;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"healthlogs-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Pet NewPet(int id, string ownerId, string name)
    {
        var now = DateTimeOffset.UtcNow;
        return new Pet
        {
            Id = id,
            OwnerId = ownerId,
            Name = name,
            SpeciesCode = "DOG",
            IsPublic = false,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static ImageAsset NewImageAsset(Guid imageId, string ownerId, string storageKey)
    {
        return new ImageAsset
        {
            ImageId = imageId,
            StorageKey = storageKey,
            ContentType = "image/jpeg",
            SizeBytes = 128,
            OwnerId = ownerId,
            Category = "HealthLog",
            Status = ImageAssetStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}

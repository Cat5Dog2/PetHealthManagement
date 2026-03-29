using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Controllers;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
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
            Note = "いつも通りでした。",
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

    [Fact]
    public async Task CreateGet_ReturnsBadRequest_WhenPetIdIsMissing()
    {
        await using var dbContext = CreateDbContext();
        var controller = BuildController(dbContext, "user-a");

        var result = await controller.Create(petId: null, returnUrl: null);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task CreateGet_ReturnsNotFound_ForNonOwnerPet()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Create(1, "/HealthLogs?petId=1");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreatePost_RedirectsToReturnUrl_WhenSuccessful()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        await dbContext.SaveChangesAsync();

        var imageService = new FakeHealthLogImageService();
        var controller = BuildController(dbContext, "user-a", imageService);

        var result = await controller.Create(new HealthLogEditViewModel
        {
            PetId = 1,
            RecordedAt = new DateTime(2026, 3, 22, 9, 30, 0),
            WeightKg = 5.8,
            FoodAmountGram = 100,
            WalkMinutes = 25,
            StoolCondition = "良好",
            Note = "朝の散歩あり",
            ReturnUrl = "/HealthLogs?petId=1&page=2"
        });

        var redirect = Assert.IsType<RedirectResult>(result);
        var savedHealthLog = await dbContext.HealthLogs.SingleAsync();

        Assert.Equal("/HealthLogs?petId=1&page=2", redirect.Url);
        Assert.Equal(1, imageService.CallCount);
        Assert.Equal(1, imageService.LastPetId);
        Assert.Equal(new DateTimeOffset(2026, 3, 22, 9, 30, 0, TimeSpan.FromHours(9)), savedHealthLog.RecordedAt);
    }

    [Fact]
    public async Task CreatePost_RollsBackAndReturnsView_WhenImageUpdateFails()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        await dbContext.SaveChangesAsync();

        var imageService = new FakeHealthLogImageService
        {
            NextResult = HealthLogImageUpdateResult.Fail("画像エラー")
        };

        var controller = BuildController(dbContext, "user-a", imageService);

        var result = await controller.Create(new HealthLogEditViewModel
        {
            PetId = 1,
            RecordedAt = new DateTime(2026, 3, 22, 9, 30, 0),
            ReturnUrl = "/HealthLogs?petId=1"
        });

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HealthLogEditViewModel>(viewResult.Model);

        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(0, await dbContext.HealthLogs.CountAsync());
        Assert.Equal("Mugi", model.PetName);
    }

    [Fact]
    public async Task EditGet_ReturnsView_ForOwner()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = new DateTimeOffset(2026, 3, 21, 9, 0, 0, TimeSpan.FromHours(9)),
            Note = "memo",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Edit(10, "/HealthLogs?petId=1");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HealthLogEditViewModel>(viewResult.Model);

        Assert.Equal(10, model.HealthLogId);
        Assert.Equal("Mugi", model.PetName);
        Assert.Equal("/HealthLogs?petId=1", model.ReturnUrl);
    }

    [Fact]
    public async Task EditPost_ReturnsNotFound_ForNonOwnerHealthLog()
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
        var result = await controller.Edit(10, new HealthLogEditViewModel
        {
            PetId = 1,
            RecordedAt = new DateTime(2026, 3, 22, 9, 0, 0)
        });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditPost_RedirectsToDefaultDetails_WhenReturnUrlIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = new DateTimeOffset(2026, 3, 21, 9, 0, 0, TimeSpan.FromHours(9)),
            WeightKg = 5.4,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var imageService = new FakeHealthLogImageService();
        var controller = BuildController(dbContext, "user-a", imageService);

        var result = await controller.Edit(10, new HealthLogEditViewModel
        {
            PetId = 1,
            RecordedAt = new DateTime(2026, 3, 23, 8, 15, 0),
            WeightKg = 5.6,
            ReturnUrl = "https://evil.example/"
        });

        var redirect = Assert.IsType<RedirectResult>(result);
        var savedHealthLog = await dbContext.HealthLogs.SingleAsync();

        Assert.Equal("/HealthLogs/Details/10", redirect.Url);
        Assert.Equal(1, imageService.CallCount);
        Assert.Equal(5.6, savedHealthLog.WeightKg);
        Assert.Equal(new DateTimeOffset(2026, 3, 23, 8, 15, 0, TimeSpan.FromHours(9)), savedHealthLog.RecordedAt);
    }

    [Fact]
    public async Task EditPost_ReturnsView_WhenImageUpdateFails()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
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

        var imageService = new FakeHealthLogImageService
        {
            NextResult = HealthLogImageUpdateResult.Fail("画像エラー")
        };

        var controller = BuildController(dbContext, "user-a", imageService);

        var result = await controller.Edit(10, new HealthLogEditViewModel
        {
            PetId = 1,
            RecordedAt = new DateTime(2026, 3, 23, 8, 15, 0)
        });

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HealthLogEditViewModel>(viewResult.Model);
        var unchangedHealthLog = await dbContext.HealthLogs.SingleAsync();

        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(10, model.HealthLogId);
        Assert.Equal(new DateTimeOffset(2026, 3, 21, 9, 0, 0, TimeSpan.FromHours(9)), unchangedHealthLog.RecordedAt);
    }

    [Fact]
    public async Task DeletePost_ReturnsBadRequest_WhenHealthLogIdIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        var controller = BuildController(dbContext, "user-a");

        var result = await controller.Delete(0, petId: null, page: null, returnUrl: null);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task DeletePost_ReturnsNotFound_ForNonOwnerHealthLog()
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
        var result = await controller.Delete(10, petId: 1, page: "2", returnUrl: "/HealthLogs?petId=1&page=2");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeletePost_RedirectsToReturnUrl_WhenLocal()
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

        var deletionService = new FakeHealthLogDeletionService();
        var controller = BuildController(dbContext, "user-a", deletionService: deletionService);

        var result = await controller.Delete(10, petId: 999, page: "3", returnUrl: "/HealthLogs?petId=1&page=3");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/HealthLogs?petId=1&page=3", redirect.Url);
        Assert.Equal(1, deletionService.CallCount);
        Assert.Equal(10, deletionService.LastHealthLogId);
    }

    [Fact]
    public async Task DeletePost_RedirectsToFallbackList_WhenReturnUrlIsInvalid()
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

        var deletionService = new FakeHealthLogDeletionService();
        var controller = BuildController(dbContext, "user-a", deletionService: deletionService);

        var result = await controller.Delete(10, petId: null, page: "2", returnUrl: "https://evil.example/");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/HealthLogs?petId=1&page=2", redirect.Url);
        Assert.Equal(1, deletionService.CallCount);
    }

    private static HealthLogsController BuildController(
        ApplicationDbContext dbContext,
        string userId,
        IHealthLogImageService? imageService = null,
        IHealthLogDeletionService? deletionService = null)
    {
        var controller = new HealthLogsController(
            dbContext,
            new OwnershipAuthorizer(dbContext),
            imageService ?? new FakeHealthLogImageService(),
            deletionService ?? new FakeHealthLogDeletionService());
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

    private sealed class FakeHealthLogImageService : IHealthLogImageService
    {
        public int CallCount { get; private set; }

        public int? LastPetId { get; private set; }

        public HealthLogImageUpdateResult NextResult { get; init; } = HealthLogImageUpdateResult.Success();

        public Task<HealthLogImageUpdateResult> ApplyImageChangesAsync(
            HealthLog healthLog,
            string ownerId,
            IReadOnlyCollection<IFormFile>? newFiles,
            IReadOnlyCollection<Guid>? deleteImageIds,
            CancellationToken cancellationToken = default)
        {
            CallCount += 1;
            LastPetId = healthLog.PetId;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeHealthLogDeletionService : IHealthLogDeletionService
    {
        public int CallCount { get; private set; }

        public int? LastHealthLogId { get; private set; }

        public Task DeleteAsync(HealthLog healthLog, string ownerId, CancellationToken cancellationToken = default)
        {
            CallCount += 1;
            LastHealthLogId = healthLog.Id;
            return Task.CompletedTask;
        }
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

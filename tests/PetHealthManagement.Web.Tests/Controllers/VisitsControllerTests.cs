using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Controllers;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.ViewModels.Visits;

namespace PetHealthManagement.Web.Tests.Controllers;

public class VisitsControllerTests
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

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task Index_ReturnsPagedVisits_ForOwner_AndNormalizesPage(string? page)
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));

        var baseDate = new DateTime(2026, 3, 21);
        for (var index = 1; index <= 11; index++)
        {
            dbContext.Visits.Add(new Visit
            {
                Id = index,
                PetId = 1,
                VisitDate = baseDate.AddDays(index <= 2 ? 0 : -index),
                ClinicName = $"Clinic {index}",
                Diagnosis = $"Diagnosis {index}",
                Prescription = $"Prescription {index}",
                Note = $"Note {index}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Index(petId: 1, page: page);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<VisitIndexViewModel>(viewResult.Model);

        Assert.Equal(1, model.Page);
        Assert.Equal(11, model.TotalCount);
        Assert.Equal(10, model.Visits.Count);
        Assert.Equal(2, model.Visits[0].VisitId);
        Assert.Equal(1, model.Visits[1].VisitId);
    }

    [Fact]
    public async Task Details_ReturnsNotFound_ForNonOwnerVisit()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.Visits.Add(new Visit
        {
            Id = 10,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Details(10, "/Visits?petId=1");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ReturnsView_WithFallbackReturnUrl_AndImages()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));

        dbContext.Visits.Add(new Visit
        {
            Id = 10,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            ClinicName = "Smile Animal Clinic",
            Diagnosis = "Seasonal allergy",
            Prescription = "Antihistamine",
            Note = "Follow up in two weeks.",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var firstImageId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var secondImageId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        dbContext.ImageAssets.AddRange(
            NewImageAsset(firstImageId, "user-a", "images/visit-1.jpg"),
            NewImageAsset(secondImageId, "user-a", "images/visit-2.jpg"));

        dbContext.VisitImages.AddRange(
            new VisitImage { Id = 1, VisitId = 10, ImageId = firstImageId, SortOrder = 2 },
            new VisitImage { Id = 2, VisitId = 10, ImageId = secondImageId, SortOrder = 1 });

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Details(10, "https://evil.example/");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<VisitDetailsViewModel>(viewResult.Model);

        Assert.Equal("Mugi", model.PetName);
        Assert.Equal("/Visits?petId=1", model.ReturnUrl);
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
        var result = await controller.Create(1, "/Visits?petId=1");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreatePost_RedirectsToReturnUrl_WhenSuccessful()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        await dbContext.SaveChangesAsync();

        var imageService = new FakeVisitImageService();
        var controller = BuildController(dbContext, "user-a", imageService);

        var result = await controller.Create(new VisitEditViewModel
        {
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 22),
            ClinicName = " Smile Animal Clinic ",
            Diagnosis = " Seasonal allergy ",
            Prescription = " Antihistamine ",
            Note = " Follow up in two weeks. ",
            ReturnUrl = "/Visits?petId=1&page=2"
        });

        var redirect = Assert.IsType<RedirectResult>(result);
        var savedVisit = await dbContext.Visits.SingleAsync();

        Assert.Equal("/Visits?petId=1&page=2", redirect.Url);
        Assert.Equal(1, imageService.CallCount);
        Assert.Equal(1, imageService.LastPetId);
        Assert.Equal(new DateTime(2026, 3, 22), savedVisit.VisitDate);
        Assert.Equal("Smile Animal Clinic", savedVisit.ClinicName);
        Assert.Equal("Seasonal allergy", savedVisit.Diagnosis);
    }

    [Fact]
    public async Task CreatePost_RollsBackAndReturnsView_WhenImageUpdateFails()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        await dbContext.SaveChangesAsync();

        var imageService = new FakeVisitImageService
        {
            NextResult = VisitImageUpdateResult.Fail("Image error")
        };

        var controller = BuildController(dbContext, "user-a", imageService);

        var result = await controller.Create(new VisitEditViewModel
        {
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 22),
            ReturnUrl = "/Visits?petId=1"
        });

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<VisitEditViewModel>(viewResult.Model);

        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(0, await dbContext.Visits.CountAsync());
        Assert.Equal("Mugi", model.PetName);
    }

    [Fact]
    public async Task EditGet_ReturnsView_ForOwner_WithExistingImages()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.Visits.Add(new Visit
        {
            Id = 10,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            ClinicName = "Clinic",
            Note = "memo",
            RowVersion = NewRowVersion(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var imageId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        dbContext.ImageAssets.Add(NewImageAsset(imageId, "user-a", "images/existing-visit.jpg"));
        dbContext.VisitImages.Add(new VisitImage
        {
            Id = 1,
            VisitId = 10,
            ImageId = imageId,
            SortOrder = 1
        });

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Edit(10, "/Visits?petId=1");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<VisitEditViewModel>(viewResult.Model);

        Assert.Equal(10, model.VisitId);
        Assert.Equal("Mugi", model.PetName);
        Assert.Equal("/Visits?petId=1", model.ReturnUrl);
        Assert.Single(model.ExistingImages);
        Assert.Equal(imageId, model.ExistingImages[0].ImageId);
        Assert.False(string.IsNullOrWhiteSpace(model.RowVersion));
    }

    [Fact]
    public async Task EditPost_ReturnsNotFound_ForNonOwnerVisit()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.Visits.Add(new Visit
        {
            Id = 10,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            RowVersion = NewRowVersion(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Edit(10, new VisitEditViewModel
        {
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 22)
        });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditPost_RedirectsToDefaultDetails_WhenReturnUrlIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.Visits.Add(new Visit
        {
            Id = 10,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            ClinicName = "Clinic",
            RowVersion = NewRowVersion(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var imageService = new FakeVisitImageService();
        var controller = BuildController(dbContext, "user-a", imageService);

        var result = await controller.Edit(10, new VisitEditViewModel
        {
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 23),
            ClinicName = "Updated Clinic",
            ReturnUrl = "https://evil.example/",
            RowVersion = EncodeRowVersion()
        });

        var redirect = Assert.IsType<RedirectResult>(result);
        var savedVisit = await dbContext.Visits.SingleAsync();

        Assert.Equal("/Visits/Details/10", redirect.Url);
        Assert.Equal(0, imageService.CallCount);
        Assert.Equal(new DateTime(2026, 3, 23), savedVisit.VisitDate);
        Assert.Equal("Updated Clinic", savedVisit.ClinicName);
    }

    [Fact]
    public async Task EditPost_ReturnsView_WhenImageUpdateFails()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.Visits.Add(new Visit
        {
            Id = 10,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            ClinicName = "Clinic",
            RowVersion = NewRowVersion(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var imageService = new FakeVisitImageService
        {
            NextResult = VisitImageUpdateResult.Fail("Image error")
        };

        var controller = BuildController(dbContext, "user-a", imageService);

        var result = await controller.Edit(10, new VisitEditViewModel
        {
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 23),
            DeleteImageIds = [Guid.NewGuid()],
            RowVersion = EncodeRowVersion()
        });

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<VisitEditViewModel>(viewResult.Model);
        var unchangedVisit = await dbContext.Visits.SingleAsync();

        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(10, model.VisitId);
        Assert.Equal(new DateTime(2026, 3, 21), unchangedVisit.VisitDate);
        Assert.Equal(1, imageService.CallCount);
    }

    [Fact]
    public async Task EditPost_ReturnsView_WhenRowVersionIsStale()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.Visits.Add(new Visit
        {
            Id = 11,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            ClinicName = "Clinic",
            RowVersion = NewRowVersion(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var imageService = new FakeVisitImageService();
        var controller = BuildController(dbContext, "user-a", imageService);

        var result = await controller.Edit(11, new VisitEditViewModel
        {
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 23),
            ClinicName = "Updated Clinic",
            RowVersion = EncodeRowVersion(version: 9)
        });

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<VisitEditViewModel>(viewResult.Model);
        var unchangedVisit = await dbContext.Visits.SingleAsync(x => x.Id == 11);

        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(
            controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == ConcurrencyMessages.RecordModified);
        Assert.Equal(11, model.VisitId);
        Assert.Equal(0, imageService.CallCount);
        Assert.Equal(new DateTime(2026, 3, 21), unchangedVisit.VisitDate);
    }

    [Fact]
    public async Task DeletePost_ReturnsBadRequest_WhenVisitIdIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        var controller = BuildController(dbContext, "user-a");

        var result = await controller.Delete(0, petId: null, page: null, returnUrl: null);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task DeletePost_ReturnsNotFound_ForNonOwnerVisit()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.Visits.Add(new Visit
        {
            Id = 10,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Delete(10, petId: "1", page: "2", returnUrl: "/Visits?petId=1&page=2");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeletePost_RedirectsToReturnUrl_WhenLocal()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.Visits.Add(new Visit
        {
            Id = 10,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var deletionService = new FakeVisitDeletionService();
        var controller = BuildController(dbContext, "user-a", deletionService: deletionService);

        var result = await controller.Delete(10, petId: "999", page: "3", returnUrl: "/Visits?petId=1&page=3");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Visits?petId=1&page=3", redirect.Url);
        Assert.Equal(1, deletionService.CallCount);
        Assert.Equal(10, deletionService.LastVisitId);
    }

    [Fact]
    public async Task DeletePost_RedirectsToFallbackList_WhenReturnUrlIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.Visits.Add(new Visit
        {
            Id = 10,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 21),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var deletionService = new FakeVisitDeletionService();
        var controller = BuildController(dbContext, "user-a", deletionService: deletionService);

        var result = await controller.Delete(10, petId: "999", page: "abc", returnUrl: "https://evil.example/");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Visits?petId=1&page=1", redirect.Url);
        Assert.Equal(1, deletionService.CallCount);
    }

    private static VisitsController BuildController(
        ApplicationDbContext dbContext,
        string userId,
        IVisitImageService? imageService = null,
        IVisitDeletionService? deletionService = null)
    {
        var controller = new VisitsController(
            dbContext,
            new OwnershipAuthorizer(dbContext),
            imageService ?? new FakeVisitImageService(),
            deletionService ?? new FakeVisitDeletionService());
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
            .UseInMemoryDatabase($"visits-tests-{Guid.NewGuid()}")
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
            Category = "Visit",
            Status = ImageAssetStatus.Ready,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class FakeVisitImageService : IVisitImageService
    {
        public int CallCount { get; private set; }

        public int? LastPetId { get; private set; }

        public VisitImageUpdateResult NextResult { get; init; } = VisitImageUpdateResult.Success();

        public Task<VisitImageUpdateResult> ApplyImageChangesAsync(
            Visit visit,
            string ownerId,
            IReadOnlyCollection<IFormFile>? newFiles,
            IReadOnlyCollection<Guid>? deleteImageIds,
            CancellationToken cancellationToken = default)
        {
            _ = ownerId;
            _ = newFiles;
            _ = deleteImageIds;
            _ = cancellationToken;

            CallCount += 1;
            LastPetId = visit.PetId;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeVisitDeletionService : IVisitDeletionService
    {
        public int CallCount { get; private set; }

        public int? LastVisitId { get; private set; }

        public Task DeleteAsync(Visit visit, string ownerId, CancellationToken cancellationToken = default)
        {
            _ = ownerId;
            _ = cancellationToken;

            CallCount += 1;
            LastVisitId = visit.Id;
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

    private static byte[] NewRowVersion(byte version = 1)
    {
        return [version, 0, 0, 0];
    }

    private static string EncodeRowVersion(byte version = 1)
    {
        return Convert.ToBase64String(NewRowVersion(version));
    }
}

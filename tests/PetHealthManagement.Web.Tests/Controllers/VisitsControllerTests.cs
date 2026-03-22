using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Controllers;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
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

    private static VisitsController BuildController(ApplicationDbContext dbContext, string userId)
    {
        var controller = new VisitsController(dbContext);
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

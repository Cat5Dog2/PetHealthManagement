using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Controllers;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.ViewModels.ScheduleItems;

namespace PetHealthManagement.Web.Tests.Controllers;

public class ScheduleItemsControllerTests
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
    public async Task Index_ReturnsPagedItems_ForOwner_AndNormalizesPage()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));

        for (var index = 1; index <= 11; index++)
        {
            dbContext.ScheduleItems.Add(new ScheduleItem
            {
                Id = index,
                PetId = 1,
                DueDate = new DateTime(2026, 4, index <= 2 ? 1 : index, 0, 0, 0),
                Type = index % 2 == 0 ? ScheduleItemTypeCatalog.Medicine : ScheduleItemTypeCatalog.Vaccine,
                Title = $"Task {index}",
                Note = $"Note {index}",
                IsDone = index % 3 == 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Index(petId: 1, page: "abc");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ScheduleItemIndexViewModel>(viewResult.Model);

        Assert.Equal(1, model.Page);
        Assert.Equal(11, model.TotalCount);
        Assert.Equal(10, model.ScheduleItems.Count);
        Assert.Equal(1, model.ScheduleItems[0].ScheduleItemId);
        Assert.Equal(2, model.ScheduleItems[1].ScheduleItemId);
        Assert.Equal("ワクチン", model.ScheduleItems[0].TypeLabel);
        Assert.False(model.ScheduleItems[0].IsDone);
    }

    [Fact]
    public async Task Details_ReturnsNotFound_ForNonOwnerScheduleItem()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.ScheduleItems.Add(new ScheduleItem
        {
            Id = 10,
            PetId = 1,
            DueDate = new DateTime(2026, 4, 1),
            Type = ScheduleItemTypeCatalog.Visit,
            Title = "定期検診",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Details(10, "/ScheduleItems?petId=1");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ReturnsView_WithFallbackReturnUrl()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.ScheduleItems.Add(new ScheduleItem
        {
            Id = 10,
            PetId = 1,
            DueDate = new DateTime(2026, 4, 15),
            Type = ScheduleItemTypeCatalog.Medicine,
            Title = "朝の投薬",
            Note = "朝食後に飲ませる",
            IsDone = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Details(10, "https://evil.example/");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ScheduleItemDetailsViewModel>(viewResult.Model);

        Assert.Equal("Mugi", model.PetName);
        Assert.Equal("投薬", model.TypeLabel);
        Assert.Equal("朝の投薬", model.Title);
        Assert.True(model.IsDone);
        Assert.Equal("/ScheduleItems?petId=1", model.ReturnUrl);
    }

    private static ScheduleItemsController BuildController(ApplicationDbContext dbContext, string userId)
    {
        var controller = new ScheduleItemsController(dbContext);
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
            .UseInMemoryDatabase($"scheduleitems-tests-{Guid.NewGuid()}")
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

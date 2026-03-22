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
                DueDate = new DateTime(2026, 4, index <= 2 ? 1 : index),
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
        Assert.Equal(ScheduleItemTypeCatalog.ToLabel(ScheduleItemTypeCatalog.Vaccine), model.ScheduleItems[0].TypeLabel);
        Assert.False(model.ScheduleItems[0].IsDone);
    }

    [Fact]
    public async Task Details_ReturnsNotFound_ForNonOwnerScheduleItem()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.ScheduleItems.Add(NewScheduleItem(10, 1, ScheduleItemTypeCatalog.Visit, "Checkup"));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Details(10, "/ScheduleItems?petId=1&page=2");

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
            Title = "Medicine",
            Note = "After breakfast",
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
        Assert.Equal(ScheduleItemTypeCatalog.ToLabel(ScheduleItemTypeCatalog.Medicine), model.TypeLabel);
        Assert.Equal("Medicine", model.Title);
        Assert.True(model.IsDone);
        Assert.Equal("/ScheduleItems?petId=1&page=1", model.ReturnUrl);
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
        var result = await controller.Create(1, "/ScheduleItems?petId=1&page=2");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreatePost_RedirectsToReturnUrl_WhenSuccessful()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");

        var result = await controller.Create(new ScheduleItemEditViewModel
        {
            PetId = 1,
            DueDate = new DateTime(2026, 5, 10),
            ItemType = "medicine",
            Title = "  Morning medicine  ",
            Note = "  After breakfast  ",
            ReturnUrl = "/ScheduleItems?petId=1&page=2"
        });

        var redirect = Assert.IsType<RedirectResult>(result);
        var savedScheduleItem = await dbContext.ScheduleItems.SingleAsync();

        Assert.Equal("/ScheduleItems?petId=1&page=2", redirect.Url);
        Assert.Equal(new DateTime(2026, 5, 10), savedScheduleItem.DueDate);
        Assert.Equal(ScheduleItemTypeCatalog.Medicine, savedScheduleItem.Type);
        Assert.Equal("Morning medicine", savedScheduleItem.Title);
        Assert.Equal("After breakfast", savedScheduleItem.Note);
        Assert.False(savedScheduleItem.IsDone);
    }

    [Fact]
    public async Task CreatePost_ReturnsView_WhenValidationFails()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");

        var result = await controller.Create(new ScheduleItemEditViewModel
        {
            PetId = 1,
            DueDate = null,
            ItemType = "BadType",
            Title = "   ",
            ReturnUrl = "/ScheduleItems?petId=1&page=1"
        });

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ScheduleItemEditViewModel>(viewResult.Model);

        Assert.False(controller.ModelState.IsValid);
        Assert.Equal("Mugi", model.PetName);
        Assert.Equal("/ScheduleItems?petId=1&page=1", model.ReturnUrl);
        Assert.Empty(await dbContext.ScheduleItems.ToListAsync());
    }

    [Fact]
    public async Task EditGet_ReturnsView_ForOwner()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.ScheduleItems.Add(new ScheduleItem
        {
            Id = 10,
            PetId = 1,
            DueDate = new DateTime(2026, 4, 20),
            Type = ScheduleItemTypeCatalog.Other,
            Title = "Memo update",
            Note = "first note",
            IsDone = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Edit(10, "/ScheduleItems?petId=1&page=2");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ScheduleItemEditViewModel>(viewResult.Model);

        Assert.Equal(10, model.ScheduleItemId);
        Assert.Equal("Mugi", model.PetName);
        Assert.Equal("/ScheduleItems?petId=1&page=2", model.ReturnUrl);
        Assert.True(model.IsDone);
    }

    [Fact]
    public async Task EditPost_ReturnsNotFound_ForNonOwnerScheduleItem()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.ScheduleItems.Add(NewScheduleItem(10, 1, ScheduleItemTypeCatalog.Vaccine, "Vaccine"));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Edit(10, new ScheduleItemEditViewModel
        {
            PetId = 1,
            DueDate = new DateTime(2026, 4, 22),
            ItemType = ScheduleItemTypeCatalog.Visit,
            Title = "Visit"
        });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditPost_RedirectsToDefaultDetails_WhenReturnUrlIsInvalid_AndKeepsIsDone()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.ScheduleItems.Add(new ScheduleItem
        {
            Id = 10,
            PetId = 1,
            DueDate = new DateTime(2026, 4, 20),
            Type = ScheduleItemTypeCatalog.Vaccine,
            Title = "Vaccine",
            Note = "old note",
            IsDone = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Edit(10, new ScheduleItemEditViewModel
        {
            PetId = 999,
            DueDate = new DateTime(2026, 4, 25),
            ItemType = "visit",
            Title = "Visit plan",
            Note = "updated note",
            IsDone = false,
            ReturnUrl = "https://evil.example/"
        });

        var redirect = Assert.IsType<RedirectResult>(result);
        var savedScheduleItem = await dbContext.ScheduleItems.SingleAsync();

        Assert.Equal("/ScheduleItems/Details/10", redirect.Url);
        Assert.Equal(new DateTime(2026, 4, 25), savedScheduleItem.DueDate);
        Assert.Equal(ScheduleItemTypeCatalog.Visit, savedScheduleItem.Type);
        Assert.Equal("Visit plan", savedScheduleItem.Title);
        Assert.Equal("updated note", savedScheduleItem.Note);
        Assert.True(savedScheduleItem.IsDone);
    }

    [Fact]
    public async Task EditPost_ReturnsView_WhenValidationFails()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.ScheduleItems.Add(new ScheduleItem
        {
            Id = 10,
            PetId = 1,
            DueDate = new DateTime(2026, 4, 20),
            Type = ScheduleItemTypeCatalog.Vaccine,
            Title = "Vaccine",
            Note = "old note",
            IsDone = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Edit(10, new ScheduleItemEditViewModel
        {
            PetId = 1,
            DueDate = new DateTime(2026, 4, 21),
            ItemType = ScheduleItemTypeCatalog.Medicine,
            Title = " ",
            ReturnUrl = "/ScheduleItems?petId=1&page=2"
        });

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ScheduleItemEditViewModel>(viewResult.Model);
        var unchangedScheduleItem = await dbContext.ScheduleItems.SingleAsync();

        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(10, model.ScheduleItemId);
        Assert.Equal("Mugi", model.PetName);
        Assert.Equal(new DateTime(2026, 4, 20), unchangedScheduleItem.DueDate);
        Assert.Equal("Vaccine", unchangedScheduleItem.Title);
    }

    [Fact]
    public async Task SetDonePost_ReturnsBadRequest_WhenIsDoneIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        var controller = BuildController(dbContext, "user-a");

        var result = await controller.SetDone(10, "abc", petId: "1", page: "2", returnUrl: null);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task SetDonePost_ReturnsNotFound_ForNonOwnerScheduleItem()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.ScheduleItems.Add(NewScheduleItem(10, 1, ScheduleItemTypeCatalog.Other, "Task"));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.SetDone(10, "true", petId: "1", page: "2", returnUrl: "/ScheduleItems?petId=1&page=2");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SetDonePost_RedirectsToReturnUrl_WhenLocal()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.ScheduleItems.Add(new ScheduleItem
        {
            Id = 10,
            PetId = 1,
            DueDate = new DateTime(2026, 4, 20),
            Type = ScheduleItemTypeCatalog.Other,
            Title = "Task",
            IsDone = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.SetDone(10, "true", petId: "999", page: "3", returnUrl: "/ScheduleItems/Details/10");

        var redirect = Assert.IsType<RedirectResult>(result);
        var savedScheduleItem = await dbContext.ScheduleItems.SingleAsync();

        Assert.Equal("/ScheduleItems/Details/10", redirect.Url);
        Assert.True(savedScheduleItem.IsDone);
    }

    [Fact]
    public async Task SetDonePost_RedirectsToFallbackList_WhenReturnUrlIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.ScheduleItems.Add(NewScheduleItem(10, 1, ScheduleItemTypeCatalog.Other, "Task"));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.SetDone(10, "false", petId: "999", page: "abc", returnUrl: "https://evil.example/");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/ScheduleItems?petId=1&page=1", redirect.Url);
    }

    [Fact]
    public async Task DeletePost_ReturnsBadRequest_WhenScheduleItemIdIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        var controller = BuildController(dbContext, "user-a");

        var result = await controller.Delete(0, petId: "1", page: "2", returnUrl: null);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task DeletePost_ReturnsNotFound_ForNonOwnerScheduleItem()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.ScheduleItems.Add(NewScheduleItem(10, 1, ScheduleItemTypeCatalog.Other, "Task"));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Delete(10, petId: "1", page: "2", returnUrl: "/ScheduleItems?petId=1&page=2");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeletePost_RedirectsToReturnUrl_WhenLocal()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.ScheduleItems.Add(NewScheduleItem(10, 1, ScheduleItemTypeCatalog.Other, "Task"));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Delete(10, petId: "999", page: "3", returnUrl: "/ScheduleItems?petId=1&page=3");

        var redirect = Assert.IsType<RedirectResult>(result);

        Assert.Equal("/ScheduleItems?petId=1&page=3", redirect.Url);
        Assert.Empty(await dbContext.ScheduleItems.ToListAsync());
    }

    [Fact]
    public async Task DeletePost_RedirectsToFallbackList_WhenReturnUrlIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Pets.Add(NewPet(1, "user-a", "Mugi"));
        dbContext.ScheduleItems.Add(NewScheduleItem(10, 1, ScheduleItemTypeCatalog.Other, "Task"));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Delete(10, petId: "999", page: "0", returnUrl: "https://evil.example/");

        var redirect = Assert.IsType<RedirectResult>(result);

        Assert.Equal("/ScheduleItems?petId=1&page=1", redirect.Url);
        Assert.Empty(await dbContext.ScheduleItems.ToListAsync());
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

    private static ScheduleItem NewScheduleItem(int id, int petId, string type, string title)
    {
        return new ScheduleItem
        {
            Id = id,
            PetId = petId,
            DueDate = new DateTime(2026, 4, 20),
            Type = type,
            Title = title,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
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

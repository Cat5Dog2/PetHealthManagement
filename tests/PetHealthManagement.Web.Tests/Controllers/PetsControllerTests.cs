using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Controllers;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.ViewModels.Pets;

namespace PetHealthManagement.Web.Tests.Controllers;

public class PetsControllerTests
{
    [Fact]
    public async Task Index_IncludesOwnPetsAndOthersPublic_AndHidesOthersPrivate()
    {
        await using var dbContext = CreateDbContext();

        dbContext.Users.AddRange(
            new IdentityUser { Id = "user-a", UserName = "userA" },
            new IdentityUser { Id = "user-b", UserName = "userB" });

        dbContext.Pets.AddRange(
            NewPet(1, "user-a", "A-Public", true),
            NewPet(2, "user-a", "A-Private", false),
            NewPet(3, "user-b", "B-Public", true),
            NewPet(4, "user-b", "B-Private", false));

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Index(nameKeyword: null, speciesFilter: null, page: null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PetSearchViewModel>(viewResult.Model);

        Assert.Equal(3, model.Pets.Count);
        Assert.Contains(model.Pets, x => x.Name == "A-Public");
        Assert.Contains(model.Pets, x => x.Name == "A-Private");
        Assert.Contains(model.Pets, x => x.Name == "B-Public");
        Assert.DoesNotContain(model.Pets, x => x.Name == "B-Private");
    }

    [Fact]
    public async Task Index_UsesPageOne_WhenPageIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new IdentityUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(1, "user-a", "A-Public", true));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Index(nameKeyword: null, speciesFilter: null, page: "abc");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PetSearchViewModel>(viewResult.Model);
        Assert.Equal(1, model.Page);
    }

    [Fact]
    public async Task Details_ReturnsNotFound_ForOthersPrivatePet()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.AddRange(
            new IdentityUser { Id = "user-a", UserName = "userA" },
            new IdentityUser { Id = "user-b", UserName = "userB" });
        dbContext.Pets.Add(NewPet(10, "user-a", "A-Private", false));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Details(petId: 10, returnUrl: "/Pets?page=2");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ReturnsView_ForOthersPublicPet()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.AddRange(
            new IdentityUser { Id = "user-a", UserName = "userA" },
            new IdentityUser { Id = "user-b", UserName = "userB" });
        dbContext.Pets.Add(NewPet(11, "user-a", "A-Public", true));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Details(petId: 11, returnUrl: "https://evil.example/");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PetDetailsViewModel>(viewResult.Model);

        Assert.False(model.IsOwner);
        Assert.Equal("/Pets", model.ReturnUrl);
    }

    [Fact]
    public async Task Details_ReturnsView_ForOwnerPrivatePet()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new IdentityUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(12, "user-a", "A-Private", false));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Details(petId: 12, returnUrl: "/Pets?page=3");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PetDetailsViewModel>(viewResult.Model);

        Assert.True(model.IsOwner);
        Assert.Equal("/Pets?page=3", model.ReturnUrl);
    }

    private static PetsController BuildController(ApplicationDbContext dbContext, string userId)
    {
        var controller = new PetsController(dbContext);
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
            .UseInMemoryDatabase($"pets-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Pet NewPet(int id, string ownerId, string name, bool isPublic)
    {
        var now = DateTimeOffset.UtcNow;
        return new Pet
        {
            Id = id,
            OwnerId = ownerId,
            Name = name,
            SpeciesCode = "DOG",
            Breed = "Shiba",
            IsPublic = isPublic,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}

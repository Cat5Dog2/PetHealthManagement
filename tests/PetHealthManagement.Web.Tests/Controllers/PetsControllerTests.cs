using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Controllers;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.ViewModels.Pets;

namespace PetHealthManagement.Web.Tests.Controllers;

public class PetsControllerTests
{
    [Fact]
    public async Task Index_IncludesOwnPetsAndOthersPublic_AndHidesOthersPrivate()
    {
        await using var dbContext = CreateDbContext();

        dbContext.Users.AddRange(
            new ApplicationUser { Id = "user-a", UserName = "userA" },
            new ApplicationUser { Id = "user-b", UserName = "userB" });

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
        Assert.All(model.Pets, x => Assert.False(string.IsNullOrWhiteSpace(x.PhotoUrl)));
    }

    [Fact]
    public async Task Index_UsesPageOne_WhenPageIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
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
            new ApplicationUser { Id = "user-a", UserName = "userA" },
            new ApplicationUser { Id = "user-b", UserName = "userB" });
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
            new ApplicationUser { Id = "user-a", UserName = "userA" },
            new ApplicationUser { Id = "user-b", UserName = "userB" });
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
        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(12, "user-a", "A-Private", false));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Details(petId: 12, returnUrl: "/Pets?page=3");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PetDetailsViewModel>(viewResult.Model);

        Assert.True(model.IsOwner);
        Assert.Equal("/Pets?page=3", model.ReturnUrl);
        Assert.False(string.IsNullOrWhiteSpace(model.PhotoUrl));
    }

    [Fact]
    public async Task Create_Post_RedirectsToMyPage_WhenReturnUrlIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var form = new PetEditViewModel
        {
            Name = "New Pet",
            SpeciesCode = "DOG",
            Breed = "Shiba",
            IsPublic = true
        };

        var result = await controller.Create(form, "https://evil.example/");

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/MyPage", redirectResult.Url);
        Assert.Equal(1, await dbContext.Pets.CountAsync());
    }

    [Fact]
    public async Task Create_Post_ReturnsView_WhenModelIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        controller.ModelState.AddModelError("Name", "Required");

        var result = await controller.Create(new PetEditViewModel(), "/Pets");

        Assert.IsType<ViewResult>(result);
        Assert.Equal(0, await dbContext.Pets.CountAsync());
    }

    [Fact]
    public async Task Edit_Post_ReturnsNotFound_ForNonOwner()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.AddRange(
            new ApplicationUser { Id = "user-a", UserName = "userA" },
            new ApplicationUser { Id = "user-b", UserName = "userB" });
        dbContext.Pets.Add(NewPet(20, "user-a", "A-Pet", true));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Edit(20, new PetEditViewModel
        {
            Name = "Updated",
            SpeciesCode = "CAT",
            IsPublic = true
        }, "/Pets");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Post_UpdatesPet_AndRedirectsToDetails_WhenReturnUrlMissing()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(21, "user-a", "Before", true));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Edit(21, new PetEditViewModel
        {
            Name = "After",
            SpeciesCode = "CAT",
            Breed = "Mix",
            IsPublic = false,
            RowVersion = EncodeRowVersion()
        }, null);

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Pets/Details/21", redirectResult.Url);

        var updated = await dbContext.Pets.SingleAsync(x => x.Id == 21);
        Assert.Equal("After", updated.Name);
        Assert.Equal("CAT", updated.SpeciesCode);
        Assert.False(updated.IsPublic);
    }

    [Fact]
    public async Task Edit_Post_ReturnsView_WhenRowVersionIsStale()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(22, "user-a", "Before", true));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Edit(22, new PetEditViewModel
        {
            Name = "After",
            SpeciesCode = "CAT",
            Breed = "Mix",
            IsPublic = false,
            RowVersion = EncodeRowVersion(version: 9)
        }, null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PetEditViewModel>(viewResult.Model);
        var savedPet = await dbContext.Pets.SingleAsync(x => x.Id == 22);

        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(
            controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == ConcurrencyMessages.RecordModified);
        Assert.Equal("Before", model.Name);
        Assert.Equal("Before", savedPet.Name);
        Assert.Equal("DOG", savedPet.SpeciesCode);
        Assert.True(savedPet.IsPublic);
    }

    [Fact]
    public async Task Delete_Post_RemovesPet_AndUsesReturnUrl()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(30, "user-a", "Delete Target", true));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Delete(30, "/Pets?page=2");

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Pets?page=2", redirectResult.Url);
        Assert.Equal(0, await dbContext.Pets.CountAsync());
    }

    [Fact]
    public async Task Delete_Post_RedirectsToMyPage_WhenReturnUrlIsInvalid()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser { Id = "user-a", UserName = "userA" });
        dbContext.Pets.Add(NewPet(32, "user-a", "Delete Target", true));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Delete(32, "https://evil.example/");

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/MyPage", redirectResult.Url);
        Assert.Equal(0, await dbContext.Pets.CountAsync());
    }

    [Fact]
    public async Task Delete_Post_ReturnsNotFound_ForNonOwner()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.AddRange(
            new ApplicationUser { Id = "user-a", UserName = "userA" },
            new ApplicationUser { Id = "user-b", UserName = "userB" });
        dbContext.Pets.Add(NewPet(31, "user-a", "A-Pet", true));
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-b");
        var result = await controller.Delete(31, "/Pets");

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(1, await dbContext.Pets.CountAsync());
    }

    private static PetsController BuildController(
        ApplicationDbContext dbContext,
        string userId,
        IPetDeletionService? petDeletionService = null)
    {
        var controller = new PetsController(
            dbContext,
            new FakePetPhotoService(),
            petDeletionService ?? new FakePetDeletionService(dbContext));
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
            RowVersion = NewRowVersion(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static byte[] NewRowVersion(byte version = 1)
    {
        return [version, 0, 0, 0];
    }

    private static string EncodeRowVersion(byte version = 1)
    {
        return Convert.ToBase64String(NewRowVersion(version));
    }

    private sealed class FakePetPhotoService : IPetPhotoService
    {
        public Task<PetPhotoUpdateResult> ApplyPetPhotoChangeAsync(
            Pet pet,
            string ownerId,
            IFormFile? newPhotoFile,
            bool removePhoto,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PetPhotoUpdateResult.Success());
        }
    }

    private sealed class FakePetDeletionService(ApplicationDbContext dbContext) : IPetDeletionService
    {
        public async Task DeleteAsync(Pet pet, string ownerId, CancellationToken cancellationToken = default)
        {
            _ = ownerId;
            dbContext.Pets.Remove(pet);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

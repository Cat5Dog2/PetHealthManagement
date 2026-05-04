using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PetHealthManagement.Web.Controllers;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Tests.Infrastructure;
using PetHealthManagement.Web.ViewModels.Home;

namespace PetHealthManagement.Web.Tests.Controllers;

public class HomeControllerTests
{
    [Fact]
    public async Task Index_UsesDefaultImages_ForAnonymousUser()
    {
        await using var dbContext = CreateDbContext();
        var controller = BuildController(dbContext, userId: null);

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeIndexViewModel>(viewResult.Model);

        Assert.Equal("/images/default/avatar-placeholder.webp", model.AvatarUrl);
        Assert.Equal("/images/default/pet-placeholder.webp", model.PetPhotoUrl);
    }

    [Fact]
    public async Task Index_UsesCurrentUserAvatarAndNewestCreatedPetPhoto()
    {
        await using var dbContext = CreateDbContext();
        var avatarImageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var olderPetPhotoImageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var newestPetPhotoImageId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var otherUserPetPhotoImageId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        dbContext.Users.AddRange(
            new ApplicationUser
            {
                Id = "user-a",
                UserName = "userA",
                AvatarImageId = avatarImageId
            },
            new ApplicationUser { Id = "user-b", UserName = "userB" });

        dbContext.Pets.AddRange(
            NewPet(1, "user-a", olderPetPhotoImageId, new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.FromHours(9))),
            NewPet(2, "user-a", newestPetPhotoImageId, new DateTimeOffset(2026, 4, 2, 9, 0, 0, TimeSpan.FromHours(9))),
            NewPet(3, "user-b", otherUserPetPhotoImageId, new DateTimeOffset(2026, 4, 3, 9, 0, 0, TimeSpan.FromHours(9))));

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeIndexViewModel>(viewResult.Model);

        Assert.Equal($"/images/{avatarImageId:D}", model.AvatarUrl);
        Assert.Equal($"/images/{newestPetPhotoImageId:D}", model.PetPhotoUrl);
    }

    private static HomeController BuildController(ApplicationDbContext dbContext, string? userId)
    {
        var controller = new HomeController(dbContext);
        var claims = userId is null
            ? []
            : new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var authenticationType = userId is null ? null : "TestAuth";

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType))
            }
        };

        return controller;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        return TestDbContextFactory.CreateInMemoryDbContext("home-controller-tests");
    }

    private static Pet NewPet(int id, string ownerId, Guid photoImageId, DateTimeOffset createdAt)
    {
        return new Pet
        {
            Id = id,
            OwnerId = ownerId,
            Name = $"Pet {id}",
            SpeciesCode = "CAT",
            IsPublic = true,
            PhotoImageId = photoImageId,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }
}

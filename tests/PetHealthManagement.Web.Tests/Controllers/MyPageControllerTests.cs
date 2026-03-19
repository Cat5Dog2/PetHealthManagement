using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Controllers;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.ViewModels.MyPage;

namespace PetHealthManagement.Web.Tests.Controllers;

public class MyPageControllerTests
{
    [Fact]
    public async Task Index_ReturnsCurrentUserAndOwnPetsOnly()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.AddRange(
            new IdentityUser { Id = "user-a", UserName = "userA", Email = "usera@example.com" },
            new IdentityUser { Id = "user-b", UserName = "userB", Email = "userb@example.com" });

        dbContext.Pets.AddRange(
            NewPet(1, "user-a", "Mugi", true),
            NewPet(2, "user-a", "Sora", false),
            NewPet(3, "user-b", "Other", true));

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MyPageViewModel>(viewResult.Model);

        Assert.Equal("userA", model.DisplayName);
        Assert.Equal("usera@example.com", model.Email);
        Assert.Equal("/images/default/avatar-placeholder.svg", model.AvatarUrl);
        Assert.Equal(2, model.Pets.Count);
        Assert.Contains(model.Pets, x => x.Name == "Mugi" && x.IsPublic);
        Assert.Contains(model.Pets, x => x.Name == "Sora" && !x.IsPublic);
        Assert.DoesNotContain(model.Pets, x => x.Name == "Other");
    }

    [Fact]
    public async Task Index_UsesFallbackValues_WhenUserNameOrEmailIsMissing()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new IdentityUser { Id = "user-a" });
        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, "user-a");
        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MyPageViewModel>(viewResult.Model);

        Assert.Equal("user-a", model.DisplayName);
        Assert.Equal("未設定", model.Email);
        Assert.Empty(model.Pets);
    }

    private static MyPageController BuildController(ApplicationDbContext dbContext, string userId)
    {
        var controller = new MyPageController(dbContext);
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
            .UseInMemoryDatabase($"mypage-tests-{Guid.NewGuid()}")
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
            IsPublic = isPublic,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Controllers;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.ViewModels.Account;

namespace PetHealthManagement.Web.Tests.Controllers;

public class AccountControllerTests
{
    [Fact]
    public async Task EditProfile_Get_ReturnsCurrentValues()
    {
        await using var dbContext = CreateDbContext();
        var avatarImageId = Guid.NewGuid();

        dbContext.Users.Add(new ApplicationUser
        {
            Id = "user-a",
            UserName = "userA",
            Email = "usera@example.com",
            DisplayName = "Hanako",
            AvatarImageId = avatarImageId
        });

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, new FakeUserAvatarService(), "user-a");
        var result = await controller.EditProfile("/Pets?page=2");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<EditProfileViewModel>(viewResult.Model);

        Assert.Equal("Hanako", model.DisplayName);
        Assert.Equal($"/images/{avatarImageId:D}", model.CurrentAvatarUrl);
        Assert.Equal("/Pets?page=2", model.ReturnUrl);
        Assert.Equal("/Pets?page=2", model.CancelUrl);
    }

    [Fact]
    public async Task EditProfile_Post_UpdatesDisplayName_AndRedirectsToReturnUrl()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser
        {
            Id = "user-a",
            UserName = "userA",
            Email = "usera@example.com",
            DisplayName = "Before"
        });

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, new FakeUserAvatarService(), "user-a");
        var result = await controller.EditProfile(new EditProfileViewModel
        {
            DisplayName = "After"
        }, "/Pets?page=2");

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Pets?page=2", redirectResult.Url);

        var updated = await dbContext.Users.SingleAsync(x => x.Id == "user-a");
        Assert.Equal("After", updated.DisplayName);
    }

    [Fact]
    public async Task EditProfile_Post_UsesFallbackDisplayName_AndSafeDefaultRedirect()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser
        {
            Id = "user-a",
            UserName = "userA",
            Email = "usera@example.com",
            DisplayName = "Before"
        });

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, new FakeUserAvatarService(), "user-a");
        var result = await controller.EditProfile(new EditProfileViewModel
        {
            DisplayName = "   "
        }, "https://evil.example/");

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/MyPage", redirectResult.Url);

        var updated = await dbContext.Users.SingleAsync(x => x.Id == "user-a");
        Assert.Equal("userA", updated.DisplayName);
    }

    [Fact]
    public async Task EditProfile_Post_ReturnsView_WhenAvatarUpdateFails()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser
        {
            Id = "user-a",
            UserName = "userA",
            DisplayName = "Before"
        });

        await dbContext.SaveChangesAsync();

        var controller = BuildController(
            dbContext,
            new FakeUserAvatarService
            {
                NextResult = UserAvatarUpdateResult.Fail("avatar failed")
            },
            "user-a");

        var result = await controller.EditProfile(new EditProfileViewModel
        {
            DisplayName = "After",
            AvatarFile = CreateFormFile("avatar.jpg", "image/jpeg")
        }, "/MyPage");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.IsType<EditProfileViewModel>(viewResult.Model);

        var updated = await dbContext.Users.SingleAsync(x => x.Id == "user-a");
        Assert.Equal("Before", updated.DisplayName);
    }

    private static AccountController BuildController(ApplicationDbContext dbContext, IUserAvatarService avatarService, string userId)
    {
        var controller = new AccountController(dbContext, avatarService);
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
            .UseInMemoryDatabase($"account-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static IFormFile CreateFormFile(string fileName, string contentType)
    {
        var stream = new MemoryStream([1, 2, 3]);
        return new FormFile(stream, 0, stream.Length, "AvatarFile", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class FakeUserAvatarService : IUserAvatarService
    {
        public UserAvatarUpdateResult NextResult { get; init; } = UserAvatarUpdateResult.Success();

        public Task<UserAvatarUpdateResult> ApplyAvatarChangeAsync(
            ApplicationUser user,
            IFormFile? newAvatarFile,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(NextResult);
        }
    }
}

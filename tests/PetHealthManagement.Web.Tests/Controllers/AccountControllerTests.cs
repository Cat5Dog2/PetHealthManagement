using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        var controller = BuildController(dbContext, new FakeUserAvatarService(), new FakeUserDataDeletionService(), "user-a");
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

        var controller = BuildController(dbContext, new FakeUserAvatarService(), new FakeUserDataDeletionService(), "user-a");
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

        var controller = BuildController(dbContext, new FakeUserAvatarService(), new FakeUserDataDeletionService(), "user-a");
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
            new FakeUserDataDeletionService(),
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

    [Fact]
    public async Task Delete_Get_ReturnsConfirmationViewModel()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser
        {
            Id = "user-a",
            UserName = "userA",
            Email = "usera@example.com",
            DisplayName = "Hanako"
        });

        await dbContext.SaveChangesAsync();

        var controller = BuildController(dbContext, new FakeUserAvatarService(), new FakeUserDataDeletionService(), "user-a");
        var result = await controller.Delete("/Pets?page=2");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeleteAccountViewModel>(viewResult.Model);

        Assert.Equal("Hanako", model.DisplayName);
        Assert.Equal("usera@example.com", model.Email);
        Assert.Equal("/Pets?page=2", model.ReturnUrl);
        Assert.Equal("/Pets?page=2", model.CancelUrl);
    }

    [Fact]
    public async Task DeleteConfirmed_Post_DeletesCurrentUser_SignsOut_AndRedirectsToRoot()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new ApplicationUser
        {
            Id = "user-a",
            UserName = "userA"
        });

        await dbContext.SaveChangesAsync();

        var deletionService = new FakeUserDataDeletionService();
        var authenticationService = new FakeAuthenticationService();
        var controller = BuildController(
            dbContext,
            new FakeUserAvatarService(),
            deletionService,
            "user-a",
            authenticationService);

        var result = await controller.DeleteConfirmed("/MyPage");

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/", redirectResult.Url);
        Assert.Equal("user-a", deletionService.DeletedUserId);
        Assert.Contains(IdentityConstants.ApplicationScheme, authenticationService.SignedOutSchemes);
    }

    private static AccountController BuildController(
        ApplicationDbContext dbContext,
        IUserAvatarService avatarService,
        IUserDataDeletionService userDataDeletionService,
        string userId,
        IAuthenticationService? authenticationService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authenticationService ?? new FakeAuthenticationService());
        services.AddSingleton<ITempDataDictionaryFactory, FakeTempDataDictionaryFactory>();

        var controller = new AccountController(dbContext, avatarService, userDataDeletionService);
        var claimsPrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal,
                RequestServices = services.BuildServiceProvider()
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

    private sealed class FakeUserDataDeletionService : IUserDataDeletionService
    {
        public string? DeletedUserId { get; private set; }

        public Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            DeletedUserId = userId;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public List<string?> SignedOutSchemes { get; } = [];

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignedOutSchemes.Add(scheme);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTempDataDictionaryFactory : ITempDataDictionaryFactory
    {
        public ITempDataDictionary GetTempData(HttpContext context)
        {
            return new TempDataDictionary(context, new FakeTempDataProvider());
        }
    }

    private sealed class FakeTempDataProvider : ITempDataProvider
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

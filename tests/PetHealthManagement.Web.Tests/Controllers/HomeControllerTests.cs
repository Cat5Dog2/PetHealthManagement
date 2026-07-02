using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PetHealthManagement.Web.Controllers;
using PetHealthManagement.Web.ViewModels.Home;

namespace PetHealthManagement.Web.Tests.Controllers;

public class HomeControllerTests
{
    [Fact]
    public void Index_UsesDefaultImages_ForAnonymousUser()
    {
        var controller = BuildController(userId: null);

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeIndexViewModel>(viewResult.Model);

        Assert.Equal("/images/default/avatar-placeholder.webp", model.AvatarUrl);
        Assert.Equal("/images/default/pet-placeholder.webp", model.PetPhotoUrl);
    }

    [Fact]
    public void Index_RedirectsAuthenticatedUserToMyPage()
    {
        var controller = BuildController("user-a");

        var result = controller.Index();

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Equal("MyPage", redirectResult.ControllerName);
    }

    private static HomeController BuildController(string? userId)
    {
        var controller = new HomeController();
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
}

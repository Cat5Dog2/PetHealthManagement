using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("MyPage")]
public class MyPageController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }
}

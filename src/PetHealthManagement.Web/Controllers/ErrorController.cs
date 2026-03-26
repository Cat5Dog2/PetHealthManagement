using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PetHealthManagement.Web.Controllers;

[AllowAnonymous]
[Route("Error")]
public class ErrorController : Controller
{
    private static readonly HashSet<int> SupportedStatusCodes = [400, 403, 404, 500];

    [Route("{statusCode:int}")]
    [SkipStatusCodePages]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index(int statusCode)
    {
        var resolvedStatusCode = SupportedStatusCodes.Contains(statusCode) ? statusCode : 500;
        Response.StatusCode = resolvedStatusCode;

        return View(resolvedStatusCode.ToString());
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Helpers;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.ViewModels.Admin.Users;

namespace PetHealthManagement.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("Admin/Users")]
public class UsersController(
    ApplicationDbContext dbContext,
    IUserDataDeletionService userDataDeletionService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? page)
    {
        var normalizedPage = NormalizePage(page);

        var query = dbContext.Users
            .AsNoTracking()
            .OrderBy(x => x.Id);

        var totalCount = await query.CountAsync();

        var pageUsers = await query
            .Skip((normalizedPage - 1) * AdminUserIndexViewModel.DefaultPageSize)
            .Take(AdminUserIndexViewModel.DefaultPageSize)
            .Select(x => new
            {
                x.Id,
                x.DisplayName,
                x.UserName,
                x.Email,
                PetCount = dbContext.Pets.Count(pet => pet.OwnerId == x.Id)
            })
            .ToListAsync();

        var viewModel = new AdminUserIndexViewModel
        {
            Page = normalizedPage,
            PageSize = AdminUserIndexViewModel.DefaultPageSize,
            TotalCount = totalCount,
            Users = pageUsers
                .Select(x => new AdminUserListItemViewModel
                {
                    UserId = x.Id,
                    DisplayName = UserDisplayNameHelper.ResolveForDisplay(
                        x.DisplayName,
                        x.UserName,
                        x.Email,
                        x.Id),
                    Email = x.Email,
                    PetCount = x.PetCount
                })
                .ToList()
        };

        return View(viewModel);
    }

    [HttpPost("Delete/{userId}")]
    public async Task<IActionResult> Delete(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return NotFound();
        }

        var deleted = await userDataDeletionService.DeleteUserAsync(userId, HttpContext.RequestAborted);
        if (!deleted)
        {
            return NotFound();
        }

        return Redirect("/Admin/Users");
    }

    private static int NormalizePage(string? page)
    {
        if (int.TryParse(page, out var parsedPage))
        {
            return PagingHelper.NormalizePage(parsedPage);
        }

        return PagingHelper.DefaultPage;
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Helpers;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.ViewModels.ScheduleItems;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("ScheduleItems")]
public class ScheduleItemsController(ApplicationDbContext dbContext) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? petId, string? page)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!petId.HasValue || petId.Value <= 0 || !ModelState.IsValid)
        {
            return BadRequest();
        }

        var pet = await dbContext.Pets
            .AsNoTracking()
            .Where(x => x.Id == petId.Value)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.OwnerId
            })
            .FirstOrDefaultAsync();

        if (pet is null || !string.Equals(pet.OwnerId, userId, StringComparison.Ordinal))
        {
            return NotFound();
        }

        var normalizedPage = NormalizePage(page);

        var query = dbContext.ScheduleItems
            .AsNoTracking()
            .Where(x => x.PetId == pet.Id)
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Id);

        var totalCount = await query.CountAsync();

        var scheduleItems = await query
            .Skip((normalizedPage - 1) * ScheduleItemIndexViewModel.DefaultPageSize)
            .Take(ScheduleItemIndexViewModel.DefaultPageSize)
            .Select(x => new
            {
                x.Id,
                x.DueDate,
                x.Type,
                x.Title,
                x.Note,
                x.IsDone
            })
            .ToListAsync();

        var viewModel = new ScheduleItemIndexViewModel
        {
            PetId = pet.Id,
            PetName = pet.Name,
            Page = normalizedPage,
            PageSize = ScheduleItemIndexViewModel.DefaultPageSize,
            TotalCount = totalCount,
            ScheduleItems = scheduleItems
                .Select(x => new ScheduleItemListItemViewModel
                {
                    ScheduleItemId = x.Id,
                    DueDate = x.DueDate,
                    TypeLabel = ScheduleItemTypeCatalog.ToLabel(x.Type),
                    Title = x.Title,
                    NoteExcerpt = ToExcerpt(x.Note),
                    IsDone = x.IsDone
                })
                .ToList()
        };

        return View(viewModel);
    }

    [HttpGet("Details/{scheduleItemId:int}")]
    public async Task<IActionResult> Details(int scheduleItemId, string? returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var scheduleItem = await dbContext.ScheduleItems
            .AsNoTracking()
            .Include(x => x.Pet)
            .FirstOrDefaultAsync(x => x.Id == scheduleItemId);

        if (scheduleItem is null || !string.Equals(scheduleItem.Pet.OwnerId, userId, StringComparison.Ordinal))
        {
            return NotFound();
        }

        var viewModel = new ScheduleItemDetailsViewModel
        {
            ScheduleItemId = scheduleItem.Id,
            PetId = scheduleItem.PetId,
            PetName = scheduleItem.Pet.Name,
            DueDate = scheduleItem.DueDate,
            TypeLabel = ScheduleItemTypeCatalog.ToLabel(scheduleItem.Type),
            Title = scheduleItem.Title,
            Note = scheduleItem.Note,
            IsDone = scheduleItem.IsDone,
            ReturnUrl = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, $"/ScheduleItems?petId={scheduleItem.PetId}")
        };

        return View(viewModel);
    }

    private static int NormalizePage(string? page)
    {
        if (int.TryParse(page, out var parsedPage))
        {
            return PagingHelper.NormalizePage(parsedPage);
        }

        return PagingHelper.DefaultPage;
    }

    private static string? ToExcerpt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= 60 ? normalized : $"{normalized[..60]}...";
    }
}

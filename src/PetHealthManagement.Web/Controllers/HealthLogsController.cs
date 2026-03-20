using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Helpers;
using PetHealthManagement.Web.ViewModels.HealthLogs;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("HealthLogs")]
public class HealthLogsController(ApplicationDbContext dbContext) : Controller
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

        var query = dbContext.HealthLogs
            .AsNoTracking()
            .Where(x => x.PetId == pet.Id)
            .OrderByDescending(x => x.RecordedAt)
            .ThenByDescending(x => x.Id);

        var totalCount = await query.CountAsync();

        var logs = await query
            .Skip((normalizedPage - 1) * HealthLogIndexViewModel.DefaultPageSize)
            .Take(HealthLogIndexViewModel.DefaultPageSize)
            .Select(x => new
            {
                x.Id,
                x.RecordedAt,
                x.WeightKg,
                x.FoodAmountGram,
                x.WalkMinutes,
                x.StoolCondition,
                x.Note,
                HasImages = x.Images.Any()
            })
            .ToListAsync();

        var viewModel = new HealthLogIndexViewModel
        {
            PetId = pet.Id,
            PetName = pet.Name,
            Page = normalizedPage,
            PageSize = HealthLogIndexViewModel.DefaultPageSize,
            TotalCount = totalCount,
            HealthLogs = logs
                .Select(x => new HealthLogListItemViewModel
                {
                    HealthLogId = x.Id,
                    RecordedAt = x.RecordedAt,
                    WeightKg = x.WeightKg,
                    FoodAmountGram = x.FoodAmountGram,
                    WalkMinutes = x.WalkMinutes,
                    StoolCondition = x.StoolCondition,
                    NoteExcerpt = ToExcerpt(x.Note),
                    HasImages = x.HasImages
                })
                .ToList()
        };

        return View(viewModel);
    }

    [HttpGet("Details/{healthLogId:int}")]
    public async Task<IActionResult> Details(int healthLogId, string? returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var healthLog = await dbContext.HealthLogs
            .AsNoTracking()
            .Include(x => x.Pet)
            .FirstOrDefaultAsync(x => x.Id == healthLogId);

        if (healthLog is null || !string.Equals(healthLog.Pet.OwnerId, userId, StringComparison.Ordinal))
        {
            return NotFound();
        }

        var images = await dbContext.HealthLogImages
            .AsNoTracking()
            .Where(x => x.HealthLogId == healthLogId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new HealthLogImageItemViewModel
            {
                ImageId = x.ImageId,
                Url = $"/images/{x.ImageId:D}",
                AltText = $"{healthLog.Pet.Name} の健康ログ画像"
            })
            .ToListAsync();

        var viewModel = new HealthLogDetailsViewModel
        {
            HealthLogId = healthLog.Id,
            PetId = healthLog.PetId,
            PetName = healthLog.Pet.Name,
            RecordedAt = healthLog.RecordedAt,
            WeightKg = healthLog.WeightKg,
            FoodAmountGram = healthLog.FoodAmountGram,
            WalkMinutes = healthLog.WalkMinutes,
            StoolCondition = healthLog.StoolCondition,
            Note = healthLog.Note,
            Images = images,
            ReturnUrl = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, $"/HealthLogs?petId={healthLog.PetId}")
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

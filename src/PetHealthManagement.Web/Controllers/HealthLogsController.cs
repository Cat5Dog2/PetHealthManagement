using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Helpers;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.ViewModels.HealthLogs;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("HealthLogs")]
public class HealthLogsController(
    ApplicationDbContext dbContext,
    IHealthLogImageService healthLogImageService,
    IHealthLogDeletionService healthLogDeletionService) : Controller
{
    private static readonly TimeSpan JstOffset = TimeSpan.FromHours(9);

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

    [HttpGet("Create")]
    public async Task<IActionResult> Create(int? petId, string? returnUrl)
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

        var pet = await LoadOwnedPetAsync(petId.Value, userId, asNoTracking: true);
        if (pet is null)
        {
            return NotFound();
        }

        return View(BuildCreateViewModel(pet, returnUrl));
    }

    [HttpPost("Create")]
    [EnableRateLimiting(UploadRateLimiting.ImageUploadPolicyName)]
    public async Task<IActionResult> Create(HealthLogEditViewModel viewModel)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var pet = await LoadOwnedPetAsync(viewModel.PetId, userId, asNoTracking: true);
        if (pet is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(BuildCreateViewModel(pet, viewModel.ReturnUrl, viewModel));
        }

        var now = DateTimeOffset.UtcNow;
        var healthLog = new HealthLog
        {
            PetId = pet.Id,
            RecordedAt = ToJstDateTimeOffset(viewModel.RecordedAt!.Value),
            WeightKg = viewModel.WeightKg,
            FoodAmountGram = viewModel.FoodAmountGram,
            WalkMinutes = viewModel.WalkMinutes,
            StoolCondition = NormalizeOptionalText(viewModel.StoolCondition),
            Note = NormalizeOptionalText(viewModel.Note),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.HealthLogs.Add(healthLog);
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        var imageUpdateResult = await healthLogImageService.ApplyImageChangesAsync(
            healthLog,
            userId,
            viewModel.NewFiles,
            deleteImageIds: [],
            HttpContext.RequestAborted);

        if (!imageUpdateResult.Succeeded)
        {
            dbContext.HealthLogs.Remove(healthLog);
            await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

            ModelState.AddModelError(nameof(HealthLogEditViewModel.NewFiles), imageUpdateResult.ErrorMessage!);
            return View(BuildCreateViewModel(pet, viewModel.ReturnUrl, viewModel));
        }

        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(viewModel.ReturnUrl, $"/HealthLogs?petId={pet.Id}");
        return Redirect(redirectUrl);
    }

    [HttpGet("Edit/{healthLogId:int}")]
    public async Task<IActionResult> Edit(int healthLogId, string? returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var healthLog = await LoadOwnedHealthLogAsync(healthLogId, userId, asNoTracking: true);
        if (healthLog is null)
        {
            return NotFound();
        }

        return View(await BuildEditViewModelAsync(healthLog, returnUrl));
    }

    [HttpPost("Edit/{healthLogId:int}")]
    [EnableRateLimiting(UploadRateLimiting.ImageUploadPolicyName)]
    public async Task<IActionResult> Edit(int healthLogId, HealthLogEditViewModel viewModel)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var healthLog = await LoadOwnedHealthLogAsync(healthLogId, userId, asNoTracking: false);
        if (healthLog is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildEditViewModelAsync(healthLog, viewModel.ReturnUrl, viewModel));
        }

        var imageUpdateResult = await healthLogImageService.ApplyImageChangesAsync(
            healthLog,
            userId,
            viewModel.NewFiles,
            viewModel.DeleteImageIds,
            HttpContext.RequestAborted);

        if (!imageUpdateResult.Succeeded)
        {
            ModelState.AddModelError(nameof(HealthLogEditViewModel.NewFiles), imageUpdateResult.ErrorMessage!);
            return View(await BuildEditViewModelAsync(healthLog, viewModel.ReturnUrl, viewModel));
        }

        healthLog.RecordedAt = ToJstDateTimeOffset(viewModel.RecordedAt!.Value);
        healthLog.WeightKg = viewModel.WeightKg;
        healthLog.FoodAmountGram = viewModel.FoodAmountGram;
        healthLog.WalkMinutes = viewModel.WalkMinutes;
        healthLog.StoolCondition = NormalizeOptionalText(viewModel.StoolCondition);
        healthLog.Note = NormalizeOptionalText(viewModel.Note);
        healthLog.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(viewModel.ReturnUrl, $"/HealthLogs/Details/{healthLogId}");
        return Redirect(redirectUrl);
    }

    [HttpPost("Delete/{healthLogId:int}")]
    public async Task<IActionResult> Delete(int healthLogId, int? petId, string? page, string? returnUrl)
    {
        _ = petId;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (healthLogId <= 0 || !ModelState.IsValid)
        {
            return BadRequest();
        }

        var healthLog = await LoadOwnedHealthLogAsync(healthLogId, userId, asNoTracking: false);
        if (healthLog is null)
        {
            return NotFound();
        }

        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(
            returnUrl,
            BuildHealthLogListUrl(healthLog.PetId, page));

        await healthLogDeletionService.DeleteAsync(healthLog, userId, HttpContext.RequestAborted);

        return Redirect(redirectUrl);
    }

    private async Task<Pet?> LoadOwnedPetAsync(int petId, string userId, bool asNoTracking)
    {
        var query = dbContext.Pets.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(x => x.Id == petId && x.OwnerId == userId);
    }

    private async Task<HealthLog?> LoadOwnedHealthLogAsync(int healthLogId, string userId, bool asNoTracking)
    {
        var query = dbContext.HealthLogs
            .Include(x => x.Pet)
            .AsQueryable();

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(x => x.Id == healthLogId && x.Pet.OwnerId == userId);
    }

    private HealthLogEditViewModel BuildCreateViewModel(Pet pet, string? returnUrl, HealthLogEditViewModel? source = null)
    {
        var safeReturnUrl = ReturnUrlHelper.IsLocalUrl(returnUrl) ? returnUrl : null;

        return new HealthLogEditViewModel
        {
            PetId = pet.Id,
            PetName = pet.Name,
            RecordedAt = source?.RecordedAt ?? DateTimeOffset.UtcNow.ToOffset(JstOffset).DateTime,
            WeightKg = source?.WeightKg,
            FoodAmountGram = source?.FoodAmountGram,
            WalkMinutes = source?.WalkMinutes,
            StoolCondition = source?.StoolCondition,
            Note = source?.Note,
            DeleteImageIds = source?.DeleteImageIds ?? [],
            ExistingImages = [],
            ReturnUrl = safeReturnUrl,
            CancelUrl = ReturnUrlHelper.ResolveLocalReturnUrl(safeReturnUrl, $"/HealthLogs?petId={pet.Id}")
        };
    }

    private async Task<HealthLogEditViewModel> BuildEditViewModelAsync(
        HealthLog healthLog,
        string? returnUrl,
        HealthLogEditViewModel? source = null)
    {
        var safeReturnUrl = ReturnUrlHelper.IsLocalUrl(returnUrl) ? returnUrl : null;

        return new HealthLogEditViewModel
        {
            HealthLogId = healthLog.Id,
            PetId = healthLog.PetId,
            PetName = healthLog.Pet.Name,
            RecordedAt = source?.RecordedAt ?? healthLog.RecordedAt.ToOffset(JstOffset).DateTime,
            WeightKg = source?.WeightKg ?? healthLog.WeightKg,
            FoodAmountGram = source?.FoodAmountGram ?? healthLog.FoodAmountGram,
            WalkMinutes = source?.WalkMinutes ?? healthLog.WalkMinutes,
            StoolCondition = source?.StoolCondition ?? healthLog.StoolCondition,
            Note = source?.Note ?? healthLog.Note,
            ExistingImages = await LoadHealthLogImagesAsync(healthLog.Id, healthLog.Pet.Name),
            DeleteImageIds = source?.DeleteImageIds ?? [],
            ReturnUrl = safeReturnUrl,
            CancelUrl = ReturnUrlHelper.ResolveLocalReturnUrl(safeReturnUrl, $"/HealthLogs/Details/{healthLog.Id}")
        };
    }

    private async Task<List<HealthLogExistingImageViewModel>> LoadHealthLogImagesAsync(int healthLogId, string petName)
    {
        return await dbContext.HealthLogImages
            .AsNoTracking()
            .Where(x => x.HealthLogId == healthLogId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new HealthLogExistingImageViewModel
            {
                ImageId = x.ImageId,
                Url = $"/images/{x.ImageId:D}",
                AltText = $"{petName} の健康ログ画像"
            })
            .ToListAsync();
    }

    private static DateTimeOffset ToJstDateTimeOffset(DateTime value)
    {
        var normalized = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        return new DateTimeOffset(normalized, JstOffset);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static int NormalizePage(string? page)
    {
        if (int.TryParse(page, out var parsedPage))
        {
            return PagingHelper.NormalizePage(parsedPage);
        }

        return PagingHelper.DefaultPage;
    }

    private static string BuildHealthLogListUrl(int petId, string? page)
    {
        var baseUrl = $"/HealthLogs?petId={petId}";

        if (int.TryParse(page, out var parsedPage) && parsedPage > 0)
        {
            return $"{baseUrl}&page={PagingHelper.NormalizePage(parsedPage)}";
        }

        return baseUrl;
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

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
using PetHealthManagement.Web.ViewModels.Visits;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("Visits")]
public class VisitsController(
    ApplicationDbContext dbContext,
    IOwnershipAuthorizer ownershipAuthorizer,
    IVisitImageService visitImageService,
    IVisitDeletionService visitDeletionService) : Controller
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

        var pet = await ownershipAuthorizer.FindOwnedPetAsync(
            petId.Value,
            userId,
            asNoTracking: true,
            HttpContext.RequestAborted);
        if (pet is null)
        {
            return NotFound();
        }

        var normalizedPage = NormalizePage(page);

        var query = dbContext.Visits
            .AsNoTracking()
            .Where(x => x.PetId == pet.Id)
            .OrderByDescending(x => x.VisitDate)
            .ThenByDescending(x => x.Id);

        var totalCount = await query.CountAsync();

        var visits = await query
            .Skip((normalizedPage - 1) * VisitIndexViewModel.DefaultPageSize)
            .Take(VisitIndexViewModel.DefaultPageSize)
            .Select(x => new
            {
                x.Id,
                x.VisitDate,
                x.ClinicName,
                x.Diagnosis,
                x.Prescription,
                x.Note,
                HasImages = x.Images.Any()
            })
            .ToListAsync();

        var viewModel = new VisitIndexViewModel
        {
            PetId = pet.Id,
            PetName = pet.Name,
            Page = normalizedPage,
            PageSize = VisitIndexViewModel.DefaultPageSize,
            TotalCount = totalCount,
            Visits = visits
                .Select(x => new VisitListItemViewModel
                {
                    VisitId = x.Id,
                    VisitDate = x.VisitDate,
                    ClinicName = x.ClinicName,
                    DiagnosisExcerpt = ToExcerpt(x.Diagnosis),
                    PrescriptionExcerpt = ToExcerpt(x.Prescription),
                    NoteExcerpt = ToExcerpt(x.Note),
                    HasImages = x.HasImages
                })
                .ToList()
        };

        return View(viewModel);
    }

    [HttpGet("Details/{visitId:int}")]
    public async Task<IActionResult> Details(int visitId, string? returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var visit = await ownershipAuthorizer.FindOwnedVisitAsync(
            visitId,
            userId,
            asNoTracking: true,
            HttpContext.RequestAborted);
        if (visit is null)
        {
            return NotFound();
        }

        var images = await dbContext.VisitImages
            .AsNoTracking()
            .Where(x => x.VisitId == visitId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new VisitImageItemViewModel
            {
                ImageId = x.ImageId,
                Url = $"/images/{x.ImageId:D}",
                AltText = $"{visit.Pet.Name} visit image"
            })
            .ToListAsync();

        var viewModel = new VisitDetailsViewModel
        {
            VisitId = visit.Id,
            PetId = visit.PetId,
            PetName = visit.Pet.Name,
            VisitDate = visit.VisitDate,
            ClinicName = visit.ClinicName,
            Diagnosis = visit.Diagnosis,
            Prescription = visit.Prescription,
            Note = visit.Note,
            Images = images,
            ReturnUrl = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, $"/Visits?petId={visit.PetId}")
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

        var pet = await ownershipAuthorizer.FindOwnedPetAsync(
            petId.Value,
            userId,
            asNoTracking: true,
            HttpContext.RequestAborted);
        if (pet is null)
        {
            return NotFound();
        }

        return View(BuildCreateViewModel(pet, returnUrl));
    }

    [HttpPost("Create")]
    [EnableRateLimiting(UploadRateLimiting.ImageUploadPolicyName)]
    public async Task<IActionResult> Create(VisitEditViewModel viewModel)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var pet = await ownershipAuthorizer.FindOwnedPetAsync(
            viewModel.PetId,
            userId,
            asNoTracking: true,
            HttpContext.RequestAborted);
        if (pet is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(BuildCreateViewModel(pet, viewModel.ReturnUrl, viewModel));
        }

        var now = DateTimeOffset.UtcNow;
        var visit = new Visit
        {
            PetId = pet.Id,
            VisitDate = NormalizeVisitDate(viewModel.VisitDate!.Value),
            ClinicName = NormalizeOptionalText(viewModel.ClinicName),
            Diagnosis = NormalizeOptionalText(viewModel.Diagnosis),
            Prescription = NormalizeOptionalText(viewModel.Prescription),
            Note = NormalizeOptionalText(viewModel.Note),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Visits.Add(visit);
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        var imageUpdateResult = await visitImageService.ApplyImageChangesAsync(
            visit,
            userId,
            viewModel.NewFiles,
            deleteImageIds: [],
            HttpContext.RequestAborted);

        if (!imageUpdateResult.Succeeded)
        {
            dbContext.Visits.Remove(visit);
            await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

            ModelState.AddModelError(nameof(VisitEditViewModel.NewFiles), imageUpdateResult.ErrorMessage!);
            return View(BuildCreateViewModel(pet, viewModel.ReturnUrl, viewModel));
        }

        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(viewModel.ReturnUrl, $"/Visits?petId={pet.Id}");
        return Redirect(redirectUrl);
    }

    [HttpGet("Edit/{visitId:int}")]
    public async Task<IActionResult> Edit(int visitId, string? returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var visit = await ownershipAuthorizer.FindOwnedVisitAsync(
            visitId,
            userId,
            asNoTracking: true,
            HttpContext.RequestAborted);
        if (visit is null)
        {
            return NotFound();
        }

        return View(await BuildEditViewModelAsync(visit, returnUrl));
    }

    [HttpPost("Edit/{visitId:int}")]
    [EnableRateLimiting(UploadRateLimiting.ImageUploadPolicyName)]
    public async Task<IActionResult> Edit(int visitId, VisitEditViewModel viewModel)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var visit = await ownershipAuthorizer.FindOwnedVisitAsync(
            visitId,
            userId,
            asNoTracking: false,
            HttpContext.RequestAborted);
        if (visit is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildEditViewModelAsync(visit, viewModel.ReturnUrl, viewModel));
        }

        var imageUpdateResult = await visitImageService.ApplyImageChangesAsync(
            visit,
            userId,
            viewModel.NewFiles,
            viewModel.DeleteImageIds,
            HttpContext.RequestAborted);

        if (!imageUpdateResult.Succeeded)
        {
            ModelState.AddModelError(nameof(VisitEditViewModel.NewFiles), imageUpdateResult.ErrorMessage!);
            return View(await BuildEditViewModelAsync(visit, viewModel.ReturnUrl, viewModel));
        }

        visit.VisitDate = NormalizeVisitDate(viewModel.VisitDate!.Value);
        visit.ClinicName = NormalizeOptionalText(viewModel.ClinicName);
        visit.Diagnosis = NormalizeOptionalText(viewModel.Diagnosis);
        visit.Prescription = NormalizeOptionalText(viewModel.Prescription);
        visit.Note = NormalizeOptionalText(viewModel.Note);
        visit.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(viewModel.ReturnUrl, $"/Visits/Details/{visitId}");
        return Redirect(redirectUrl);
    }

    [HttpPost("Delete/{visitId:int}")]
    public async Task<IActionResult> Delete(int visitId, string? petId, string? page, string? returnUrl)
    {
        _ = petId;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (visitId <= 0)
        {
            return BadRequest();
        }

        var visit = await ownershipAuthorizer.FindOwnedVisitAsync(
            visitId,
            userId,
            asNoTracking: false,
            HttpContext.RequestAborted);
        if (visit is null)
        {
            return NotFound();
        }

        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(
            returnUrl,
            BuildVisitListUrl(visit.PetId, page));

        await visitDeletionService.DeleteAsync(visit, userId, HttpContext.RequestAborted);

        return Redirect(redirectUrl);
    }

    private VisitEditViewModel BuildCreateViewModel(Pet pet, string? returnUrl, VisitEditViewModel? source = null)
    {
        var safeReturnUrl = ReturnUrlHelper.IsLocalUrl(returnUrl) ? returnUrl : null;

        return new VisitEditViewModel
        {
            PetId = pet.Id,
            PetName = pet.Name,
            VisitDate = source?.VisitDate ?? DateTimeOffset.UtcNow.ToOffset(JstOffset).Date,
            ClinicName = source?.ClinicName,
            Diagnosis = source?.Diagnosis,
            Prescription = source?.Prescription,
            Note = source?.Note,
            ExistingImages = [],
            DeleteImageIds = source?.DeleteImageIds ?? [],
            ReturnUrl = safeReturnUrl,
            CancelUrl = ReturnUrlHelper.ResolveLocalReturnUrl(safeReturnUrl, $"/Visits?petId={pet.Id}")
        };
    }

    private async Task<VisitEditViewModel> BuildEditViewModelAsync(
        Visit visit,
        string? returnUrl,
        VisitEditViewModel? source = null)
    {
        var safeReturnUrl = ReturnUrlHelper.IsLocalUrl(returnUrl) ? returnUrl : null;

        return new VisitEditViewModel
        {
            VisitId = visit.Id,
            PetId = visit.PetId,
            PetName = visit.Pet.Name,
            VisitDate = source?.VisitDate ?? visit.VisitDate,
            ClinicName = source?.ClinicName ?? visit.ClinicName,
            Diagnosis = source?.Diagnosis ?? visit.Diagnosis,
            Prescription = source?.Prescription ?? visit.Prescription,
            Note = source?.Note ?? visit.Note,
            ExistingImages = await LoadVisitImagesAsync(visit.Id, visit.Pet.Name),
            DeleteImageIds = source?.DeleteImageIds ?? [],
            ReturnUrl = safeReturnUrl,
            CancelUrl = ReturnUrlHelper.ResolveLocalReturnUrl(safeReturnUrl, $"/Visits/Details/{visit.Id}")
        };
    }

    private async Task<List<VisitExistingImageViewModel>> LoadVisitImagesAsync(int visitId, string petName)
    {
        return await dbContext.VisitImages
            .AsNoTracking()
            .Where(x => x.VisitId == visitId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new VisitExistingImageViewModel
            {
                ImageId = x.ImageId,
                Url = $"/images/{x.ImageId:D}",
                AltText = $"{petName} visit image"
            })
            .ToListAsync();
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

    private static DateTime NormalizeVisitDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string BuildVisitListUrl(int petId, string? page)
    {
        var baseUrl = $"/Visits?petId={petId}";
        if (string.IsNullOrWhiteSpace(page))
        {
            return baseUrl;
        }

        if (int.TryParse(page, out var parsedPage) && parsedPage > 0)
        {
            return $"{baseUrl}&page={PagingHelper.NormalizePage(parsedPage)}";
        }

        return $"{baseUrl}&page={PagingHelper.DefaultPage}";
    }
}

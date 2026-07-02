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

        var normalizedPage = PagingHelper.NormalizePage(page);

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
                    DiagnosisExcerpt = StringFormatter.ToExcerpt(x.Diagnosis),
                    PrescriptionExcerpt = StringFormatter.ToExcerpt(x.Prescription),
                    NoteExcerpt = StringFormatter.ToExcerpt(x.Note),
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
            ReturnUrl = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, PetActivityUrlHelper.VisitList(visit.PetId))
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
            ClinicName = StringFormatter.NormalizeOptionalText(viewModel.ClinicName),
            Diagnosis = StringFormatter.NormalizeOptionalText(viewModel.Diagnosis),
            Prescription = StringFormatter.NormalizeOptionalText(viewModel.Prescription),
            Note = StringFormatter.NormalizeOptionalText(viewModel.Note),
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

            if (imageUpdateResult.IsConcurrencyConflict)
            {
                ModelState.AddModelError(string.Empty, ConcurrencyMessages.RecordModified);
            }
            else
            {
                ModelState.AddModelError(
                    nameof(VisitEditViewModel.NewFiles),
                    imageUpdateResult.ErrorMessage ?? ImageUploadErrorMessages.SaveFailed);
            }

            return View(BuildCreateViewModel(pet, viewModel.ReturnUrl, viewModel));
        }

        TempData[StatusMessages.TempDataKey] = StatusMessages.VisitCreated;
        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(viewModel.ReturnUrl, PetActivityUrlHelper.VisitList(pet.Id));
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

        if (!RowVersionCodec.TryDecode(viewModel.RowVersion, out var postedRowVersion))
        {
            return BadRequest();
        }

        if (!RowVersionCodec.HasExpectedRowVersion(visit.RowVersion, postedRowVersion))
        {
            return await BuildConcurrencyConflictResultAsync(visit, viewModel.ReturnUrl);
        }

        // Run image changes before mutating this tracked record; the image service saves internally.
        var imageChangeResult = await ApplyImageChangesForEditAsync(visitId, visit, userId, viewModel);
        if (imageChangeResult is not null)
        {
            return imageChangeResult;
        }

        dbContext.Entry(visit).Property(x => x.RowVersion).OriginalValue = postedRowVersion;
        ApplyVisitEditValues(visit, viewModel);

        try
        {
            await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            var currentVisit = await ownershipAuthorizer.FindOwnedVisitAsync(
                visitId,
                userId,
                asNoTracking: true,
                HttpContext.RequestAborted);
            if (currentVisit is null)
            {
                return NotFound();
            }

            return await BuildConcurrencyConflictResultAsync(currentVisit, viewModel.ReturnUrl);
        }

        TempData[StatusMessages.TempDataKey] = StatusMessages.VisitUpdated;
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
            PetActivityUrlHelper.VisitList(visit.PetId, page));

        await visitDeletionService.DeleteAsync(visit, userId, HttpContext.RequestAborted);

        TempData[StatusMessages.TempDataKey] = StatusMessages.VisitDeleted;
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
            RowVersion = source?.RowVersion,
            ReturnUrl = safeReturnUrl,
            CancelUrl = ReturnUrlHelper.ResolveLocalReturnUrl(safeReturnUrl, PetActivityUrlHelper.VisitList(pet.Id))
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
            RowVersion = source?.RowVersion ?? RowVersionCodec.Encode(visit.RowVersion),
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



    private static DateTime NormalizeVisitDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);
    }



    private async Task<ViewResult> BuildConcurrencyConflictResultAsync(Visit visit, string? returnUrl)
    {
        ModelState.Clear();
        ModelState.AddModelError(string.Empty, ConcurrencyMessages.RecordModified);
        return View("Edit", await BuildEditViewModelAsync(visit, returnUrl));
    }

    private async Task<IActionResult?> ApplyImageChangesForEditAsync(
        int visitId,
        Visit visit,
        string userId,
        VisitEditViewModel viewModel)
    {
        if (!HasImageChanges(viewModel))
        {
            return null;
        }

        var imageUpdateResult = await visitImageService.ApplyImageChangesAsync(
            visit,
            userId,
            viewModel.NewFiles,
            viewModel.DeleteImageIds,
            HttpContext.RequestAborted);

        if (imageUpdateResult.IsConcurrencyConflict)
        {
            var currentVisit = await ownershipAuthorizer.FindOwnedVisitAsync(
                visitId,
                userId,
                asNoTracking: true,
                HttpContext.RequestAborted);
            if (currentVisit is null)
            {
                return NotFound();
            }

            return await BuildConcurrencyConflictResultAsync(currentVisit, viewModel.ReturnUrl);
        }

        if (imageUpdateResult.Succeeded)
        {
            return null;
        }

        ModelState.AddModelError(nameof(VisitEditViewModel.NewFiles), imageUpdateResult.ErrorMessage!);
        return View(await BuildEditViewModelAsync(visit, viewModel.ReturnUrl, viewModel));
    }

    private static bool HasImageChanges(VisitEditViewModel viewModel)
    {
        return viewModel.NewFiles?.Count > 0 || (viewModel.DeleteImageIds?.Length ?? 0) > 0;
    }

    private static void ApplyVisitEditValues(Visit visit, VisitEditViewModel viewModel)
    {
        visit.VisitDate = NormalizeVisitDate(viewModel.VisitDate!.Value);
        visit.ClinicName = StringFormatter.NormalizeOptionalText(viewModel.ClinicName);
        visit.Diagnosis = StringFormatter.NormalizeOptionalText(viewModel.Diagnosis);
        visit.Prescription = StringFormatter.NormalizeOptionalText(viewModel.Prescription);
        visit.Note = StringFormatter.NormalizeOptionalText(viewModel.Note);
        visit.UpdatedAt = DateTimeOffset.UtcNow;
    }
}

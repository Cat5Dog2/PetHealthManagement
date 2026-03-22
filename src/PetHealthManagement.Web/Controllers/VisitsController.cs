using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Helpers;
using PetHealthManagement.Web.ViewModels.Visits;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("Visits")]
public class VisitsController(ApplicationDbContext dbContext) : Controller
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

        var visit = await dbContext.Visits
            .AsNoTracking()
            .Include(x => x.Pet)
            .FirstOrDefaultAsync(x => x.Id == visitId);

        if (visit is null || !string.Equals(visit.Pet.OwnerId, userId, StringComparison.Ordinal))
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

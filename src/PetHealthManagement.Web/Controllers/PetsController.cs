using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Helpers;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.ViewModels.Pets;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("Pets")]
public class PetsController(
    ApplicationDbContext dbContext,
    IPetPhotoService petPhotoService) : Controller
{
    private const string DefaultPetPhotoUrl = "/images/default/pet-placeholder.svg";

    [HttpGet("")]
    public async Task<IActionResult> Index(string? nameKeyword, string? speciesFilter, string? page)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var normalizedPage = NormalizePage(page);
        var normalizedKeyword = NormalizeKeyword(nameKeyword);
        var normalizedSpeciesFilter = NormalizeSpeciesFilter(speciesFilter);

        var query = dbContext.Pets
            .AsNoTracking()
            .Where(p => p.OwnerId == userId || p.IsPublic);

        if (!string.IsNullOrEmpty(normalizedKeyword))
        {
            query = query.Where(p => p.Name.Contains(normalizedKeyword));
        }

        if (!string.IsNullOrEmpty(normalizedSpeciesFilter))
        {
            query = query.Where(p => p.SpeciesCode == normalizedSpeciesFilter);
        }

        query = query
            .OrderByDescending(p => p.UpdatedAt)
            .ThenByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id);

        var totalCount = await query.CountAsync();

        var pagePets = await query
            .Skip((normalizedPage - 1) * PetSearchViewModel.DefaultPageSize)
            .Take(PetSearchViewModel.DefaultPageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.SpeciesCode,
                p.Breed,
                p.OwnerId,
                p.IsPublic,
                p.PhotoImageId
            })
            .ToListAsync();

        var ownerIds = pagePets
            .Select(p => p.OwnerId)
            .Distinct()
            .ToList();

        var ownerMap = await dbContext.Users
            .AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => UserDisplayNameHelper.ResolveForDisplay(u));

        var viewModel = new PetSearchViewModel
        {
            NameKeyword = normalizedKeyword,
            SpeciesFilter = normalizedSpeciesFilter,
            Page = normalizedPage,
            PageSize = PetSearchViewModel.DefaultPageSize,
            TotalCount = totalCount,
            SpeciesOptions = SpeciesCatalog.All
                .Select(x => new SpeciesOptionViewModel
                {
                    Code = x.Code,
                    Label = x.Label
                })
                .ToList(),
            Pets = pagePets
                .Select(p => new PetListItemViewModel
                {
                    PetId = p.Id,
                    PhotoUrl = ResolvePetPhotoUrl(p.PhotoImageId),
                    Name = p.Name,
                    SpeciesLabel = SpeciesCatalog.ToLabel(p.SpeciesCode),
                    Breed = p.Breed,
                    OwnerDisplayName = ownerMap.GetValueOrDefault(p.OwnerId, p.OwnerId),
                    IsPublic = p.IsPublic,
                    IsOwner = p.OwnerId == userId
                })
                .ToList()
        };

        return View(viewModel);
    }

    [HttpGet("Details/{petId:int}")]
    public async Task<IActionResult> Details(int petId, string? returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var pet = await dbContext.Pets
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == petId);

        if (pet is null)
        {
            return NotFound();
        }

        var isOwner = pet.OwnerId == userId;
        if (!isOwner && !pet.IsPublic)
        {
            return NotFound();
        }

        var owner = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == pet.OwnerId)
            .FirstOrDefaultAsync();

        var ownerDisplayName = owner is null
            ? pet.OwnerId
            : UserDisplayNameHelper.ResolveForDisplay(owner);

        var viewModel = new PetDetailsViewModel
        {
            PetId = pet.Id,
            PhotoUrl = ResolvePetPhotoUrl(pet.PhotoImageId),
            Name = pet.Name,
            SpeciesLabel = SpeciesCatalog.ToLabel(pet.SpeciesCode),
            Breed = pet.Breed,
            OwnerDisplayName = ownerDisplayName,
            IsPublic = pet.IsPublic,
            IsOwner = isOwner,
            ReturnUrl = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, "/Pets")
        };

        return View(viewModel);
    }

    [HttpGet("Create")]
    public IActionResult Create(string? returnUrl)
    {
        var viewModel = BuildPetEditViewModel(
            petId: null,
            name: string.Empty,
            speciesCode: string.Empty,
            breed: null,
            isPublic: true,
            currentPhotoUrl: null,
            returnUrl: returnUrl,
            fallbackCancelUrl: "/MyPage");

        return View(viewModel);
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(PetEditViewModel viewModel, string? returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        ValidateSpeciesCode(viewModel.SpeciesCode);
        if (!ModelState.IsValid)
        {
            var invalidViewModel = BuildPetEditViewModel(
                petId: null,
                name: viewModel.Name,
                speciesCode: viewModel.SpeciesCode,
                breed: viewModel.Breed,
                isPublic: viewModel.IsPublic,
                currentPhotoUrl: null,
                returnUrl: returnUrl,
                fallbackCancelUrl: "/MyPage");

            return View(invalidViewModel);
        }

        var now = DateTimeOffset.UtcNow;
        var pet = new Pet
        {
            OwnerId = userId,
            Name = viewModel.Name.Trim(),
            SpeciesCode = viewModel.SpeciesCode.Trim().ToUpperInvariant(),
            Breed = NormalizeBreed(viewModel.Breed),
            IsPublic = viewModel.IsPublic,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Pets.Add(pet);
        await dbContext.SaveChangesAsync(cancellationToken: HttpContext.RequestAborted);

        var photoUpdateResult = await petPhotoService.ApplyPetPhotoChangeAsync(
            pet,
            userId,
            viewModel.PhotoFile,
            viewModel.RemovePhoto,
            HttpContext.RequestAborted);

        if (!photoUpdateResult.Succeeded)
        {
            dbContext.Pets.Remove(pet);
            await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

            ModelState.AddModelError(nameof(PetEditViewModel.PhotoFile), photoUpdateResult.ErrorMessage!);
            var invalidPhotoViewModel = BuildPetEditViewModel(
                petId: null,
                name: viewModel.Name,
                speciesCode: viewModel.SpeciesCode,
                breed: viewModel.Breed,
                isPublic: viewModel.IsPublic,
                currentPhotoUrl: null,
                returnUrl: returnUrl,
                fallbackCancelUrl: "/MyPage");

            return View(invalidPhotoViewModel);
        }

        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, "/MyPage");
        return Redirect(redirectUrl);
    }

    [HttpGet("Edit/{petId:int}")]
    public async Task<IActionResult> Edit(int petId, string? returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var pet = await dbContext.Pets
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == petId);

        if (pet is null || pet.OwnerId != userId)
        {
            return NotFound();
        }

        var viewModel = BuildPetEditViewModel(
            petId: pet.Id,
            name: pet.Name,
            speciesCode: pet.SpeciesCode,
            breed: pet.Breed,
            isPublic: pet.IsPublic,
            currentPhotoUrl: ResolvePetPhotoUrl(pet.PhotoImageId),
            returnUrl: returnUrl,
            fallbackCancelUrl: $"/Pets/Details/{petId}");

        return View(viewModel);
    }

    [HttpPost("Edit/{petId:int}")]
    public async Task<IActionResult> Edit(int petId, PetEditViewModel viewModel, string? returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var pet = await dbContext.Pets
            .FirstOrDefaultAsync(p => p.Id == petId);

        if (pet is null || pet.OwnerId != userId)
        {
            return NotFound();
        }

        ValidateSpeciesCode(viewModel.SpeciesCode);
        if (!ModelState.IsValid)
        {
            var invalidViewModel = BuildPetEditViewModel(
                petId: petId,
                name: viewModel.Name,
                speciesCode: viewModel.SpeciesCode,
                breed: viewModel.Breed,
                isPublic: viewModel.IsPublic,
                currentPhotoUrl: ResolvePetPhotoUrl(pet.PhotoImageId),
                returnUrl: returnUrl,
                fallbackCancelUrl: $"/Pets/Details/{petId}");

            return View(invalidViewModel);
        }

        var photoUpdateResult = await petPhotoService.ApplyPetPhotoChangeAsync(
            pet,
            userId,
            viewModel.PhotoFile,
            viewModel.RemovePhoto,
            HttpContext.RequestAborted);

        if (!photoUpdateResult.Succeeded)
        {
            ModelState.AddModelError(nameof(PetEditViewModel.PhotoFile), photoUpdateResult.ErrorMessage!);
            var invalidPhotoViewModel = BuildPetEditViewModel(
                petId: petId,
                name: viewModel.Name,
                speciesCode: viewModel.SpeciesCode,
                breed: viewModel.Breed,
                isPublic: viewModel.IsPublic,
                currentPhotoUrl: ResolvePetPhotoUrl(pet.PhotoImageId),
                returnUrl: returnUrl,
                fallbackCancelUrl: $"/Pets/Details/{petId}");

            return View(invalidPhotoViewModel);
        }

        pet.Name = viewModel.Name.Trim();
        pet.SpeciesCode = viewModel.SpeciesCode.Trim().ToUpperInvariant();
        pet.Breed = NormalizeBreed(viewModel.Breed);
        pet.IsPublic = viewModel.IsPublic;
        pet.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, $"/Pets/Details/{petId}");
        return Redirect(redirectUrl);
    }

    [HttpPost("Delete/{petId:int}")]
    public async Task<IActionResult> Delete(int petId, string? returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var pet = await dbContext.Pets
            .FirstOrDefaultAsync(p => p.Id == petId);

        if (pet is null || pet.OwnerId != userId)
        {
            return NotFound();
        }

        await petPhotoService.ApplyPetPhotoChangeAsync(
            pet,
            userId,
            newPhotoFile: null,
            removePhoto: true,
            HttpContext.RequestAborted);

        dbContext.Pets.Remove(pet);
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, "/MyPage");
        return Redirect(redirectUrl);
    }

    private static PetEditViewModel BuildPetEditViewModel(
        int? petId,
        string name,
        string speciesCode,
        string? breed,
        bool isPublic,
        string? currentPhotoUrl,
        string? returnUrl,
        string fallbackCancelUrl)
    {
        var safeReturnUrl = ReturnUrlHelper.IsLocalUrl(returnUrl) ? returnUrl : null;
        var cancelUrl = ReturnUrlHelper.ResolveLocalReturnUrl(safeReturnUrl, fallbackCancelUrl);

        return new PetEditViewModel
        {
            PetId = petId,
            Name = name,
            SpeciesCode = speciesCode,
            Breed = breed,
            IsPublic = isPublic,
            CurrentPhotoUrl = currentPhotoUrl,
            ReturnUrl = safeReturnUrl,
            CancelUrl = cancelUrl,
            SpeciesOptions = SpeciesCatalog.All
                .Select(x => new SpeciesOptionViewModel
                {
                    Code = x.Code,
                    Label = x.Label
                })
                .ToList()
        };
    }

    private void ValidateSpeciesCode(string? speciesCode)
    {
        if (!SpeciesCatalog.IsKnownCode(speciesCode))
        {
            ModelState.AddModelError(nameof(PetEditViewModel.SpeciesCode), "種別を選択してください。");
        }
    }

    private static string? NormalizeBreed(string? breed)
    {
        var normalized = breed?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string ResolvePetPhotoUrl(Guid? photoImageId)
    {
        return photoImageId is null ? DefaultPetPhotoUrl : $"/images/{photoImageId.Value:D}";
    }

    private static int NormalizePage(string? page)
    {
        if (int.TryParse(page, out var parsedPage))
        {
            return PagingHelper.NormalizePage(parsedPage);
        }

        return PagingHelper.DefaultPage;
    }

    private static string? NormalizeKeyword(string? nameKeyword)
    {
        var normalized = nameKeyword?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeSpeciesFilter(string? speciesFilter)
    {
        var normalized = speciesFilter?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized.ToUpperInvariant();
    }
}

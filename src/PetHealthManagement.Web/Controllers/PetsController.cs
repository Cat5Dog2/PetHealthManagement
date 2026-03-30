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
using PetHealthManagement.Web.ViewModels.Pets;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("Pets")]
public class PetsController(
    ApplicationDbContext dbContext,
    IPetPhotoService petPhotoService,
    IPetDeletionService petDeletionService) : Controller
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

        var pageQuery = query
            .Skip((normalizedPage - 1) * PetSearchViewModel.DefaultPageSize)
            .Take(PetSearchViewModel.DefaultPageSize);

        var pagePets = await (
            from pet in pageQuery
            join owner in dbContext.Users.AsNoTracking() on pet.OwnerId equals owner.Id into ownerGroup
            from owner in ownerGroup.DefaultIfEmpty()
            select new
            {
                pet.Id,
                pet.Name,
                pet.SpeciesCode,
                pet.Breed,
                pet.OwnerId,
                pet.IsPublic,
                pet.PhotoImageId,
                OwnerDisplayName = owner != null ? owner.DisplayName : null,
                OwnerUserName = owner != null ? owner.UserName : null,
                OwnerEmail = owner != null ? owner.Email : null
            })
            .ToListAsync();

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
                    OwnerDisplayName = UserDisplayNameHelper.ResolveForDisplay(
                        p.OwnerDisplayName,
                        p.OwnerUserName,
                        p.OwnerEmail,
                        p.OwnerId),
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
            rowVersion: null,
            returnUrl: returnUrl,
            fallbackCancelUrl: "/MyPage");

        return View(viewModel);
    }

    [HttpPost("Create")]
    [EnableRateLimiting(UploadRateLimiting.ImageUploadPolicyName)]
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
                rowVersion: null,
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
                rowVersion: null,
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
            rowVersion: RowVersionCodec.Encode(pet.RowVersion),
            returnUrl: returnUrl,
            fallbackCancelUrl: $"/Pets/Details/{petId}");

        return View(viewModel);
    }

    [HttpPost("Edit/{petId:int}")]
    [EnableRateLimiting(UploadRateLimiting.ImageUploadPolicyName)]
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
                rowVersion: viewModel.RowVersion,
                returnUrl: returnUrl,
                fallbackCancelUrl: $"/Pets/Details/{petId}");

            return View(invalidViewModel);
        }

        if (!RowVersionCodec.TryDecode(viewModel.RowVersion, out var postedRowVersion))
        {
            return BadRequest();
        }

        if (!HasExpectedRowVersion(pet.RowVersion, postedRowVersion))
        {
            return BuildConcurrencyConflictResult(pet, returnUrl);
        }

        dbContext.Entry(pet).Property(x => x.RowVersion).OriginalValue = postedRowVersion;
        var originalValues = CapturePetEditValues(pet);
        ApplyPetEditValues(pet, viewModel);

        if (HasPhotoChange(viewModel))
        {
            PetPhotoUpdateResult photoUpdateResult;
            try
            {
                photoUpdateResult = await petPhotoService.ApplyPetPhotoChangeAsync(
                    pet,
                    userId,
                    viewModel.PhotoFile,
                    viewModel.RemovePhoto,
                    HttpContext.RequestAborted);
            }
            catch (DbUpdateConcurrencyException)
            {
                var currentPet = await dbContext.Pets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == petId && x.OwnerId == userId, HttpContext.RequestAborted);
                if (currentPet is null)
                {
                    return NotFound();
                }

                return BuildConcurrencyConflictResult(currentPet, returnUrl);
            }

            if (!photoUpdateResult.Succeeded)
            {
                try
                {
                    RestorePetEditValues(pet, originalValues);
                    await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
                }
                catch (DbUpdateConcurrencyException)
                {
                    var currentPet = await dbContext.Pets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == petId && x.OwnerId == userId, HttpContext.RequestAborted);
                    if (currentPet is null)
                    {
                        return NotFound();
                    }

                    return BuildConcurrencyConflictResult(currentPet, returnUrl);
                }

                ModelState.AddModelError(nameof(PetEditViewModel.PhotoFile), photoUpdateResult.ErrorMessage!);
                var invalidPhotoViewModel = BuildPetEditViewModel(
                    petId: petId,
                    name: viewModel.Name,
                    speciesCode: viewModel.SpeciesCode,
                    breed: viewModel.Breed,
                    isPublic: viewModel.IsPublic,
                    currentPhotoUrl: ResolvePetPhotoUrl(pet.PhotoImageId),
                    rowVersion: RowVersionCodec.Encode(pet.RowVersion),
                    returnUrl: returnUrl,
                    fallbackCancelUrl: $"/Pets/Details/{petId}");

                return View(invalidPhotoViewModel);
            }

            var photoRedirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, $"/Pets/Details/{petId}");
            return Redirect(photoRedirectUrl);
        }

        try
        {
            await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            var currentPet = await dbContext.Pets
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == petId && x.OwnerId == userId, HttpContext.RequestAborted);
            if (currentPet is null)
            {
                return NotFound();
            }

            return BuildConcurrencyConflictResult(currentPet, returnUrl);
        }

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

        await petDeletionService.DeleteAsync(pet, userId, HttpContext.RequestAborted);

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
        string? rowVersion,
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
            RowVersion = rowVersion,
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

    private ViewResult BuildConcurrencyConflictResult(Pet pet, string? returnUrl)
    {
        ModelState.Clear();
        ModelState.AddModelError(string.Empty, ConcurrencyMessages.RecordModified);
        return View("Edit", BuildPetEditViewModel(
            petId: pet.Id,
            name: pet.Name,
            speciesCode: pet.SpeciesCode,
            breed: pet.Breed,
            isPublic: pet.IsPublic,
            currentPhotoUrl: ResolvePetPhotoUrl(pet.PhotoImageId),
            rowVersion: RowVersionCodec.Encode(pet.RowVersion),
            returnUrl: returnUrl,
            fallbackCancelUrl: $"/Pets/Details/{pet.Id}"));
    }

    private static bool HasExpectedRowVersion(byte[]? currentRowVersion, byte[] postedRowVersion)
    {
        return currentRowVersion is not null
            && currentRowVersion.AsSpan().SequenceEqual(postedRowVersion);
    }

    private static bool HasPhotoChange(PetEditViewModel viewModel)
    {
        return viewModel.PhotoFile is not null || viewModel.RemovePhoto;
    }

    private static void ApplyPetEditValues(Pet pet, PetEditViewModel viewModel)
    {
        pet.Name = viewModel.Name.Trim();
        pet.SpeciesCode = viewModel.SpeciesCode.Trim().ToUpperInvariant();
        pet.Breed = NormalizeBreed(viewModel.Breed);
        pet.IsPublic = viewModel.IsPublic;
        pet.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static PetEditSnapshot CapturePetEditValues(Pet pet)
    {
        return new PetEditSnapshot(
            pet.Name,
            pet.SpeciesCode,
            pet.Breed,
            pet.IsPublic,
            pet.UpdatedAt);
    }

    private static void RestorePetEditValues(Pet pet, PetEditSnapshot snapshot)
    {
        pet.Name = snapshot.Name;
        pet.SpeciesCode = snapshot.SpeciesCode;
        pet.Breed = snapshot.Breed;
        pet.IsPublic = snapshot.IsPublic;
        pet.UpdatedAt = snapshot.UpdatedAt;
    }

    private sealed record PetEditSnapshot(
        string Name,
        string SpeciesCode,
        string? Breed,
        bool IsPublic,
        DateTimeOffset UpdatedAt);
}

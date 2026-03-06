using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Helpers;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.ViewModels.Pets;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
public class PetsController(ApplicationDbContext dbContext) : Controller
{
    [HttpGet]
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
                p.IsPublic
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
                u => string.IsNullOrWhiteSpace(u.UserName)
                    ? (string.IsNullOrWhiteSpace(u.Email) ? u.Id : u.Email)
                    : u.UserName);

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

    [HttpGet]
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

        var ownerDisplayName = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == pet.OwnerId)
            .Select(u => string.IsNullOrWhiteSpace(u.UserName)
                ? (string.IsNullOrWhiteSpace(u.Email) ? u.Id : u.Email)
                : u.UserName)
            .FirstOrDefaultAsync()
            ?? pet.OwnerId;

        var viewModel = new PetDetailsViewModel
        {
            PetId = pet.Id,
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

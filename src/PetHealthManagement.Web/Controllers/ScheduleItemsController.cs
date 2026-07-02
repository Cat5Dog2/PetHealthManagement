using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Helpers;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.ViewModels.ScheduleItems;

namespace PetHealthManagement.Web.Controllers;

[Authorize]
[Route("ScheduleItems")]
public class ScheduleItemsController(ApplicationDbContext dbContext) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? petId, string? page, string? typeFilter = null)
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

        var normalizedPage = PagingHelper.NormalizePage(page);
        var normalizedTypeFilter = ScheduleItemTypeCatalog.NormalizeFilterCode(typeFilter);

        var query = dbContext.ScheduleItems
            .AsNoTracking()
            .Where(x => x.PetId == pet.Id);

        if (!string.IsNullOrEmpty(normalizedTypeFilter))
        {
            query = query.Where(x => x.Type == normalizedTypeFilter);
        }

        query = query
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
            TypeFilter = normalizedTypeFilter,
            Page = normalizedPage,
            PageSize = ScheduleItemIndexViewModel.DefaultPageSize,
            TotalCount = totalCount,
            TypeOptions = BuildTypeOptions(),
            ScheduleItems = scheduleItems
                .Select(x => new ScheduleItemListItemViewModel
                {
                    ScheduleItemId = x.Id,
                    DueDate = x.DueDate,
                    TypeLabel = ScheduleItemTypeCatalog.ToLabel(x.Type),
                    Title = x.Title,
                    NoteExcerpt = StringFormatter.ToExcerpt(x.Note),
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
            ReturnUrl = ReturnUrlHelper.ResolveLocalReturnUrl(returnUrl, PetActivityUrlHelper.ScheduleItemList(scheduleItem.PetId))
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
    public async Task<IActionResult> Create(ScheduleItemEditViewModel viewModel)
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

        ValidateScheduleItemInput(viewModel);
        if (!ModelState.IsValid)
        {
            return View(BuildCreateViewModel(pet, viewModel.ReturnUrl, viewModel));
        }

        var now = DateTimeOffset.UtcNow;
        var scheduleItem = new ScheduleItem
        {
            PetId = pet.Id,
            DueDate = viewModel.DueDate!.Value.Date,
            Type = ScheduleItemTypeCatalog.NormalizeKnownCode(viewModel.ItemType),
            Title = StringFormatter.NormalizeRequiredText(viewModel.Title),
            Note = StringFormatter.NormalizeOptionalText(viewModel.Note),
            IsDone = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ScheduleItems.Add(scheduleItem);
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        TempData[StatusMessages.TempDataKey] = StatusMessages.ScheduleItemCreated;
        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(viewModel.ReturnUrl, PetActivityUrlHelper.ScheduleItemList(pet.Id));
        return Redirect(redirectUrl);
    }

    [HttpGet("Edit/{scheduleItemId:int}")]
    public async Task<IActionResult> Edit(int scheduleItemId, string? returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var scheduleItem = await LoadOwnedScheduleItemAsync(scheduleItemId, userId, asNoTracking: true);
        if (scheduleItem is null)
        {
            return NotFound();
        }

        return View(BuildEditViewModel(scheduleItem, returnUrl));
    }

    [HttpPost("Edit/{scheduleItemId:int}")]
    public async Task<IActionResult> Edit(int scheduleItemId, ScheduleItemEditViewModel viewModel)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var scheduleItem = await LoadOwnedScheduleItemAsync(scheduleItemId, userId, asNoTracking: false);
        if (scheduleItem is null)
        {
            return NotFound();
        }

        ValidateScheduleItemInput(viewModel);
        if (!ModelState.IsValid)
        {
            return View(BuildEditViewModel(scheduleItem, viewModel.ReturnUrl, viewModel));
        }

        if (!RowVersionCodec.TryDecode(viewModel.RowVersion, out var postedRowVersion))
        {
            return BadRequest();
        }

        if (!RowVersionCodec.HasExpectedRowVersion(scheduleItem.RowVersion, postedRowVersion))
        {
            return BuildConcurrencyConflictResult(scheduleItem, viewModel.ReturnUrl);
        }

        dbContext.Entry(scheduleItem).Property(x => x.RowVersion).OriginalValue = postedRowVersion;
        scheduleItem.DueDate = viewModel.DueDate!.Value.Date;
        scheduleItem.Type = ScheduleItemTypeCatalog.NormalizeKnownCode(viewModel.ItemType);
        scheduleItem.Title = StringFormatter.NormalizeRequiredText(viewModel.Title);
        scheduleItem.Note = StringFormatter.NormalizeOptionalText(viewModel.Note);
        scheduleItem.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            var currentScheduleItem = await LoadOwnedScheduleItemAsync(scheduleItemId, userId, asNoTracking: true);
            if (currentScheduleItem is null)
            {
                return NotFound();
            }

            return BuildConcurrencyConflictResult(currentScheduleItem, viewModel.ReturnUrl);
        }

        TempData[StatusMessages.TempDataKey] = StatusMessages.ScheduleItemUpdated;
        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(viewModel.ReturnUrl, $"/ScheduleItems/Details/{scheduleItemId}");
        return Redirect(redirectUrl);
    }

    [HttpPost("SetDone/{scheduleItemId:int}")]
    public async Task<IActionResult> SetDone(int scheduleItemId, string? isDone, string? petId, string? page, string? returnUrl)
    {
        _ = petId;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (scheduleItemId <= 0 || !TryParseIsDone(isDone, out var parsedIsDone))
        {
            return BadRequest();
        }

        var scheduleItem = await LoadOwnedScheduleItemAsync(scheduleItemId, userId, asNoTracking: false);
        if (scheduleItem is null)
        {
            return NotFound();
        }

        scheduleItem.IsDone = parsedIsDone;
        scheduleItem.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        TempData[StatusMessages.TempDataKey] = parsedIsDone
            ? StatusMessages.ScheduleItemMarkedDone
            : StatusMessages.ScheduleItemMarkedNotDone;
        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(
            returnUrl,
            PetActivityUrlHelper.ScheduleItemList(scheduleItem.PetId, page));

        return Redirect(redirectUrl);
    }

    [HttpPost("Delete/{scheduleItemId:int}")]
    public async Task<IActionResult> Delete(int scheduleItemId, string? petId, string? page, string? returnUrl)
    {
        _ = petId;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (scheduleItemId <= 0)
        {
            return BadRequest();
        }

        var scheduleItem = await LoadOwnedScheduleItemAsync(scheduleItemId, userId, asNoTracking: false);
        if (scheduleItem is null)
        {
            return NotFound();
        }

        var redirectUrl = ReturnUrlHelper.ResolveLocalReturnUrl(
            returnUrl,
            PetActivityUrlHelper.ScheduleItemList(scheduleItem.PetId, page));

        dbContext.ScheduleItems.Remove(scheduleItem);
        await dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        TempData[StatusMessages.TempDataKey] = StatusMessages.ScheduleItemDeleted;
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

    private async Task<ScheduleItem?> LoadOwnedScheduleItemAsync(int scheduleItemId, string userId, bool asNoTracking)
    {
        var query = dbContext.ScheduleItems
            .Include(x => x.Pet)
            .AsQueryable();

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(x => x.Id == scheduleItemId && x.Pet.OwnerId == userId);
    }

    private ScheduleItemEditViewModel BuildCreateViewModel(
        Pet pet,
        string? returnUrl,
        ScheduleItemEditViewModel? source = null)
    {
        var safeReturnUrl = ReturnUrlHelper.IsLocalUrl(returnUrl) ? returnUrl : null;

        return new ScheduleItemEditViewModel
        {
            PetId = pet.Id,
            PetName = pet.Name,
            DueDate = source?.DueDate ?? DateTime.Today,
            ItemType = source?.ItemType ?? string.Empty,
            Title = source?.Title ?? string.Empty,
            Note = source?.Note,
            IsDone = false,
            ReturnUrl = safeReturnUrl,
            CancelUrl = ReturnUrlHelper.ResolveLocalReturnUrl(safeReturnUrl, PetActivityUrlHelper.ScheduleItemList(pet.Id)),
            TypeOptions = BuildTypeOptions()
        };
    }

    private ScheduleItemEditViewModel BuildEditViewModel(
        ScheduleItem scheduleItem,
        string? returnUrl,
        ScheduleItemEditViewModel? source = null)
    {
        var safeReturnUrl = ReturnUrlHelper.IsLocalUrl(returnUrl) ? returnUrl : null;

        return new ScheduleItemEditViewModel
        {
            ScheduleItemId = scheduleItem.Id,
            PetId = scheduleItem.PetId,
            PetName = scheduleItem.Pet.Name,
            DueDate = source?.DueDate ?? scheduleItem.DueDate,
            ItemType = source?.ItemType ?? scheduleItem.Type,
            Title = source?.Title ?? scheduleItem.Title,
            Note = source?.Note ?? scheduleItem.Note,
            IsDone = scheduleItem.IsDone,
            RowVersion = source?.RowVersion ?? RowVersionCodec.Encode(scheduleItem.RowVersion),
            ReturnUrl = safeReturnUrl,
            CancelUrl = ReturnUrlHelper.ResolveLocalReturnUrl(safeReturnUrl, $"/ScheduleItems/Details/{scheduleItem.Id}"),
            TypeOptions = BuildTypeOptions()
        };
    }

    private void ValidateScheduleItemInput(ScheduleItemEditViewModel viewModel)
    {
        if (!viewModel.DueDate.HasValue)
        {
            ModelState.AddModelError(nameof(ScheduleItemEditViewModel.DueDate), "期日を入力してください。");
        }

        if (!ScheduleItemTypeCatalog.IsKnownCode(viewModel.ItemType))
        {
            ModelState.AddModelError(nameof(ScheduleItemEditViewModel.ItemType), "種別を選択してください。");
        }

        var normalizedTitle = viewModel.Title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            ModelState.AddModelError(nameof(ScheduleItemEditViewModel.Title), "タイトルを入力してください。");
        }
        else if (normalizedTitle.Length > 100)
        {
            ModelState.AddModelError(nameof(ScheduleItemEditViewModel.Title), "タイトルは100文字以内で入力してください。");
        }

        var normalizedNote = viewModel.Note?.Trim();
        if (!string.IsNullOrEmpty(normalizedNote) && normalizedNote.Length > 1000)
        {
            ModelState.AddModelError(nameof(ScheduleItemEditViewModel.Note), "メモは1000文字以内で入力してください。");
        }
    }

    private static List<ScheduleItemTypeOptionViewModel> BuildTypeOptions()
    {
        return ScheduleItemTypeCatalog.All
            .Select(x => new ScheduleItemTypeOptionViewModel
            {
                Code = x.Code,
                Label = x.Label
            })
            .ToList();
    }



    private static bool TryParseIsDone(string? isDone, out bool parsedIsDone)
    {
        return bool.TryParse(isDone, out parsedIsDone);
    }

    private ViewResult BuildConcurrencyConflictResult(ScheduleItem scheduleItem, string? returnUrl)
    {
        ModelState.Clear();
        ModelState.AddModelError(string.Empty, ConcurrencyMessages.RecordModified);
        return View("Edit", BuildEditViewModel(scheduleItem, returnUrl));
    }
}

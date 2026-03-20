namespace PetHealthManagement.Web.ViewModels.ScheduleItems;

public class ScheduleItemIndexViewModel
{
    public const int DefaultPageSize = 10;

    public int PetId { get; set; }

    public string PetName { get; set; } = string.Empty;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    public int TotalCount { get; set; }

    public List<ScheduleItemListItemViewModel> ScheduleItems { get; set; } = [];

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}

public class ScheduleItemListItemViewModel
{
    public int ScheduleItemId { get; set; }

    public DateTime DueDate { get; set; }

    public string TypeLabel { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? NoteExcerpt { get; set; }

    public bool IsDone { get; set; }
}

namespace PetHealthManagement.Web.ViewModels.HealthLogs;

public class HealthLogIndexViewModel
{
    public const int DefaultPageSize = 10;

    public int PetId { get; set; }

    public string PetName { get; set; } = string.Empty;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    public int TotalCount { get; set; }

    public List<HealthLogListItemViewModel> HealthLogs { get; set; } = [];

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}

public class HealthLogListItemViewModel
{
    public int HealthLogId { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public double? WeightKg { get; set; }

    public int? FoodAmountGram { get; set; }

    public int? WalkMinutes { get; set; }

    public string? StoolCondition { get; set; }

    public string? NoteExcerpt { get; set; }

    public bool HasImages { get; set; }
}

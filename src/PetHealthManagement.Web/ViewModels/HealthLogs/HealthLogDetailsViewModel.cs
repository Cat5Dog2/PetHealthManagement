namespace PetHealthManagement.Web.ViewModels.HealthLogs;

public class HealthLogDetailsViewModel
{
    public int HealthLogId { get; set; }

    public int PetId { get; set; }

    public string PetName { get; set; } = string.Empty;

    public DateTimeOffset RecordedAt { get; set; }

    public double? WeightKg { get; set; }

    public int? FoodAmountGram { get; set; }

    public int? WalkMinutes { get; set; }

    public string? StoolCondition { get; set; }

    public string? Note { get; set; }

    public List<HealthLogImageItemViewModel> Images { get; set; } = [];

    public string ReturnUrl { get; set; } = "/MyPage";
}

public class HealthLogImageItemViewModel
{
    public Guid ImageId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string AltText { get; set; } = string.Empty;
}

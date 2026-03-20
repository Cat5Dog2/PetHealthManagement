using System.ComponentModel.DataAnnotations;

namespace PetHealthManagement.Web.ViewModels.HealthLogs;

public class HealthLogEditViewModel
{
    public int? HealthLogId { get; set; }

    public int PetId { get; set; }

    public string PetName { get; set; } = string.Empty;

    [Required]
    public DateTime? RecordedAt { get; set; }

    [Range(0.0, 200.0)]
    public double? WeightKg { get; set; }

    [Range(0, 5000)]
    public int? FoodAmountGram { get; set; }

    [Range(0, 1440)]
    public int? WalkMinutes { get; set; }

    [StringLength(50)]
    public string? StoolCondition { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    public List<HealthLogExistingImageViewModel> ExistingImages { get; set; } = [];

    public List<IFormFile> NewFiles { get; set; } = [];

    public Guid[] DeleteImageIds { get; set; } = [];

    public string? ReturnUrl { get; set; }

    public string CancelUrl { get; set; } = "/MyPage";
}

public class HealthLogExistingImageViewModel
{
    public Guid ImageId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string AltText { get; set; } = string.Empty;
}

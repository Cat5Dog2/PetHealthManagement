using System.ComponentModel.DataAnnotations;
using PetHealthManagement.Web.Infrastructure;

namespace PetHealthManagement.Web.ViewModels.HealthLogs;

public class HealthLogEditViewModel
{
    public int? HealthLogId { get; set; }

    public int PetId { get; set; }

    public string PetName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.DateTime)]
    [DisplayFormat(DataFormatString = "{0:" + InputValidationLimits.DateTimeLocalInputFormat + "}", ApplyFormatInEditMode = true)]
    public DateTime? RecordedAt { get; set; }

    [Range(InputValidationLimits.HealthLogs.WeightKgMin, InputValidationLimits.HealthLogs.WeightKgMax)]
    public double? WeightKg { get; set; }

    [Range(InputValidationLimits.HealthLogs.FoodAmountGramMin, InputValidationLimits.HealthLogs.FoodAmountGramMax)]
    public int? FoodAmountGram { get; set; }

    [Range(InputValidationLimits.HealthLogs.WalkMinutesMin, InputValidationLimits.HealthLogs.WalkMinutesMax)]
    public int? WalkMinutes { get; set; }

    [StringLength(InputValidationLimits.HealthLogs.StoolConditionMaxLength)]
    public string? StoolCondition { get; set; }

    [StringLength(InputValidationLimits.HealthLogs.NoteMaxLength)]
    public string? Note { get; set; }

    public List<HealthLogExistingImageViewModel> ExistingImages { get; set; } = [];

    public List<IFormFile> NewFiles { get; set; } = [];

    public Guid[] DeleteImageIds { get; set; } = [];

    public string? RowVersion { get; set; }

    public string? ReturnUrl { get; set; }

    public string CancelUrl { get; set; } = "/MyPage";
}

public class HealthLogExistingImageViewModel
{
    public Guid ImageId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string AltText { get; set; } = string.Empty;
}

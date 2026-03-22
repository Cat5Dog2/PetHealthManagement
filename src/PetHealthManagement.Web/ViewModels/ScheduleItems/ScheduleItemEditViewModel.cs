using System.ComponentModel.DataAnnotations;

namespace PetHealthManagement.Web.ViewModels.ScheduleItems;

public class ScheduleItemEditViewModel
{
    public int? ScheduleItemId { get; set; }

    public int PetId { get; set; }

    public string PetName { get; set; } = string.Empty;

    [Required]
    public DateTime? DueDate { get; set; }

    [Required]
    [StringLength(20)]
    public string ItemType { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Note { get; set; }

    public bool IsDone { get; set; }

    public string? ReturnUrl { get; set; }

    public string CancelUrl { get; set; } = "/MyPage";

    public List<ScheduleItemTypeOptionViewModel> TypeOptions { get; set; } = [];
}

public class ScheduleItemTypeOptionViewModel
{
    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

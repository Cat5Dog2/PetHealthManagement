namespace PetHealthManagement.Web.ViewModels.ScheduleItems;

public class ScheduleItemDetailsViewModel
{
    public int ScheduleItemId { get; set; }

    public int PetId { get; set; }

    public string PetName { get; set; } = string.Empty;

    public DateTime DueDate { get; set; }

    public string TypeLabel { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Note { get; set; }

    public bool IsDone { get; set; }

    public string ReturnUrl { get; set; } = "/MyPage";
}

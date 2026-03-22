namespace PetHealthManagement.Web.ViewModels.Visits;

public class VisitDetailsViewModel
{
    public int VisitId { get; set; }

    public int PetId { get; set; }

    public string PetName { get; set; } = string.Empty;

    public DateTime VisitDate { get; set; }

    public string? ClinicName { get; set; }

    public string? Diagnosis { get; set; }

    public string? Prescription { get; set; }

    public string? Note { get; set; }

    public List<VisitImageItemViewModel> Images { get; set; } = [];

    public string ReturnUrl { get; set; } = "/MyPage";
}

public class VisitImageItemViewModel
{
    public Guid ImageId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string AltText { get; set; } = string.Empty;
}

namespace PetHealthManagement.Web.ViewModels.Visits;

public class VisitIndexViewModel
{
    public const int DefaultPageSize = 10;

    public int PetId { get; set; }

    public string PetName { get; set; } = string.Empty;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    public int TotalCount { get; set; }

    public List<VisitListItemViewModel> Visits { get; set; } = [];

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}

public class VisitListItemViewModel
{
    public int VisitId { get; set; }

    public DateTime VisitDate { get; set; }

    public string? ClinicName { get; set; }

    public string? DiagnosisExcerpt { get; set; }

    public string? PrescriptionExcerpt { get; set; }

    public string? NoteExcerpt { get; set; }

    public bool HasImages { get; set; }
}

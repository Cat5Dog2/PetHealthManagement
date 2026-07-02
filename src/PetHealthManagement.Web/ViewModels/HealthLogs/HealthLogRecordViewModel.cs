namespace PetHealthManagement.Web.ViewModels.HealthLogs;

public class HealthLogRecordViewModel
{
    public List<HealthLogRecordPetViewModel> Pets { get; set; } = [];
}

public class HealthLogRecordPetViewModel
{
    public int PetId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SpeciesLabel { get; set; } = string.Empty;

    public string PhotoUrl { get; set; } = string.Empty;
}

namespace PetHealthManagement.Web.ViewModels.MyPage;

public class MyPageViewModel
{
    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string AvatarUrl { get; set; } = string.Empty;

    public List<MyPetSummaryViewModel> Pets { get; set; } = [];
}

public class MyPetSummaryViewModel
{
    public int PetId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SpeciesLabel { get; set; } = string.Empty;

    public string PhotoUrl { get; set; } = string.Empty;

    public bool IsPublic { get; set; }
}

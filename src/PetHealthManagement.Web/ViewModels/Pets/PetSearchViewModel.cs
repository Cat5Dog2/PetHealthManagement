namespace PetHealthManagement.Web.ViewModels.Pets;

public class PetSearchViewModel
{
    public const int DefaultPageSize = 10;

    public string? NameKeyword { get; set; }

    public string? SpeciesFilter { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    public int TotalCount { get; set; }

    public List<PetListItemViewModel> Pets { get; set; } = [];

    public List<SpeciesOptionViewModel> SpeciesOptions { get; set; } = [];

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}

public class PetListItemViewModel
{
    public int PetId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SpeciesLabel { get; set; } = string.Empty;

    public string? Breed { get; set; }

    public string OwnerDisplayName { get; set; } = string.Empty;

    public bool IsPublic { get; set; }

    public bool IsOwner { get; set; }
}

public class SpeciesOptionViewModel
{
    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

namespace PetHealthManagement.Web.Models;

public static class StoolConditionCatalog
{
    public static readonly IReadOnlyList<string> Options =
    [
        "良好",
        "普通",
        "軟便",
        "下痢",
        "血便",
        "便秘"
    ];

    public static bool IsKnownOption(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && Options.Contains(value);
    }
}

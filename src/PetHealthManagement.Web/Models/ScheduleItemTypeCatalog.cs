namespace PetHealthManagement.Web.Models;

public static class ScheduleItemTypeCatalog
{
    public const string Vaccine = "Vaccine";
    public const string Medicine = "Medicine";
    public const string Visit = "Visit";
    public const string Other = "Other";

    public static readonly IReadOnlyList<ScheduleItemTypeItem> All =
    [
        new(Vaccine, "ワクチン"),
        new(Medicine, "投薬"),
        new(Visit, "通院"),
        new(Other, "その他")
    ];

    public static bool IsKnownCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return All.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public static string ToLabel(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var hit = All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        return hit is null ? code : hit.Label;
    }

    public sealed record ScheduleItemTypeItem(string Code, string Label);
}

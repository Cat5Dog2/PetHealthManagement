namespace PetHealthManagement.Web.Models;

public static class SpeciesCatalog
{
    public static readonly IReadOnlyList<SpeciesItem> All =
    [
        new("DOG", "犬"),
        new("CAT", "猫"),
        new("HAMSTER_GUINEA_PIG", "ハムスター・モルモット"),
        new("RABBIT", "うさぎ"),
        new("OTHER_MAMMAL", "その他の哺乳類"),
        new("BIRD", "小鳥"),
        new("FISH", "お魚"),
        new("TURTLE", "亀"),
        new("REPTILE_AMPHIBIAN", "爬虫類・両生類"),
        new("INSECT", "昆虫")
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

    public sealed record SpeciesItem(string Code, string Label);
}

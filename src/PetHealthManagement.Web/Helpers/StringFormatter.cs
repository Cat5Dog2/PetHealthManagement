namespace PetHealthManagement.Web.Helpers;

public static class StringFormatter
{
    public static string? ToExcerpt(string? value, int maxLength = 60)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength]}...";
    }

    public static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    public static string NormalizeRequiredText(string value)
    {
        return value.Trim();
    }
}

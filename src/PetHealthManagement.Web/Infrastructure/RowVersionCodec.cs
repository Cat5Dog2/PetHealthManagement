namespace PetHealthManagement.Web.Infrastructure;

public static class RowVersionCodec
{
    public static string? Encode(byte[]? rowVersion)
    {
        return rowVersion is null || rowVersion.Length == 0
            ? null
            : Convert.ToBase64String(rowVersion);
    }

    public static bool TryDecode(string? encodedRowVersion, out byte[] rowVersion)
    {
        rowVersion = [];

        if (string.IsNullOrWhiteSpace(encodedRowVersion))
        {
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(encodedRowVersion);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

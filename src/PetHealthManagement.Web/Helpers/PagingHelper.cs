namespace PetHealthManagement.Web.Helpers;

public static class PagingHelper
{
    public const int DefaultPage = 1;

    public static int NormalizePage(int? page)
    {
        return page is > 0 ? page.Value : DefaultPage;
    }

    public static int NormalizePage(string? page)
    {
        if (int.TryParse(page, out var parsedPage))
        {
            return NormalizePage(parsedPage);
        }

        return DefaultPage;
    }
}

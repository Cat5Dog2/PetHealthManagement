namespace PetHealthManagement.Web.Helpers;

public static class PagingHelper
{
    public const int DefaultPage = 1;

    public static int NormalizePage(int? page)
    {
        return page is > 0 ? page.Value : DefaultPage;
    }
}

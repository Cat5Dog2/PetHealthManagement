using PetHealthManagement.Web.Infrastructure;

namespace PetHealthManagement.Web.Helpers;

public static class PetActivityUrlHelper
{
    public static string HealthLogList(int petId, string? page = null)
    {
        return BuildPetScopedListUrl(
            "/HealthLogs",
            petId,
            page,
            includeDefaultPageWhenMissing: false,
            includeDefaultPageWhenInvalid: false);
    }

    public static string VisitList(int petId, string? page = null)
    {
        return BuildPetScopedListUrl(
            "/Visits",
            petId,
            page,
            includeDefaultPageWhenMissing: false,
            includeDefaultPageWhenInvalid: true);
    }

    public static string ScheduleItemList(int petId, string? page = null)
    {
        return BuildPetScopedListUrl(
            "/ScheduleItems",
            petId,
            page,
            includeDefaultPageWhenMissing: true,
            includeDefaultPageWhenInvalid: true);
    }

    private static string BuildPetScopedListUrl(
        string path,
        int petId,
        string? page,
        bool includeDefaultPageWhenMissing,
        bool includeDefaultPageWhenInvalid)
    {
        var baseUrl = $"{path}?petId={petId}";
        if (string.IsNullOrWhiteSpace(page))
        {
            return includeDefaultPageWhenMissing
                ? $"{baseUrl}&page={PagingHelper.DefaultPage}"
                : baseUrl;
        }

        if (int.TryParse(page, out var parsedPage) && parsedPage > 0)
        {
            return $"{baseUrl}&page={PagingHelper.NormalizePage(parsedPage)}";
        }

        return includeDefaultPageWhenInvalid
            ? $"{baseUrl}&page={PagingHelper.DefaultPage}"
            : baseUrl;
    }
}

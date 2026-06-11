namespace PetHealthManagement.Web.E2ETests;

internal sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (!IsEnabled())
        {
            Skip = "Set RUN_PLAYWRIGHT_E2E=1 to run Playwright E2E tests.";
        }
    }

    private static bool IsEnabled()
    {
        var value = Environment.GetEnvironmentVariable("RUN_PLAYWRIGHT_E2E");

        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}

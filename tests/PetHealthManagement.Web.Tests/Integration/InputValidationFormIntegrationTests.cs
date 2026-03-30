using System.Net;
using System.Text.RegularExpressions;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Integration;

public class InputValidationFormIntegrationTests
{
    [Fact]
    public async Task EditProfileAndPetCreate_RenderConfiguredMaxLengthAttributes()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnerAndPet(dbContext);
            return Task.CompletedTask;
        });

        using var client = factory.CreateAuthenticatedClient("owner-user");

        using var editProfileResponse = await client.GetAsync("/Account/EditProfile");
        Assert.Equal(HttpStatusCode.OK, editProfileResponse.StatusCode);
        var editProfileHtml = await editProfileResponse.Content.ReadAsStringAsync();
        AssertElementHasAttribute(
            editProfileHtml,
            "DisplayName",
            "maxlength",
            InputValidationLimits.Profile.DisplayNameMaxLength.ToString());

        using var createPetResponse = await client.GetAsync("/Pets/Create");
        Assert.Equal(HttpStatusCode.OK, createPetResponse.StatusCode);
        var createPetHtml = await createPetResponse.Content.ReadAsStringAsync();
        AssertElementHasAttribute(
            createPetHtml,
            "Name",
            "maxlength",
            InputValidationLimits.Pets.NameMaxLength.ToString());
        AssertElementHasAttribute(
            createPetHtml,
            "Breed",
            "maxlength",
            InputValidationLimits.Pets.BreedMaxLength.ToString());
    }

    [Fact]
    public async Task HealthLogsCreate_RendersConfiguredDateTimeAndNumericConstraints()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnerAndPet(dbContext);
            return Task.CompletedTask;
        });

        using var client = factory.CreateAuthenticatedClient("owner-user");
        using var response = await client.GetAsync("/HealthLogs/Create?petId=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        AssertElementHasAttribute(html, "RecordedAt", "type", "datetime-local");
        AssertElementHasAttribute(
            html,
            "WeightKg",
            "min",
            InputValidationLimits.HealthLogs.WeightKgMin.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AssertElementHasAttribute(
            html,
            "WeightKg",
            "max",
            InputValidationLimits.HealthLogs.WeightKgMax.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AssertElementHasAttribute(
            html,
            "WeightKg",
            "step",
            InputValidationLimits.HealthLogs.WeightKgStep.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        AssertElementHasAttribute(
            html,
            "FoodAmountGram",
            "min",
            InputValidationLimits.HealthLogs.FoodAmountGramMin.ToString());
        AssertElementHasAttribute(
            html,
            "FoodAmountGram",
            "max",
            InputValidationLimits.HealthLogs.FoodAmountGramMax.ToString());
        AssertElementHasAttribute(
            html,
            "WalkMinutes",
            "min",
            InputValidationLimits.HealthLogs.WalkMinutesMin.ToString());
        AssertElementHasAttribute(
            html,
            "WalkMinutes",
            "max",
            InputValidationLimits.HealthLogs.WalkMinutesMax.ToString());
        AssertElementHasAttribute(
            html,
            "StoolCondition",
            "maxlength",
            InputValidationLimits.HealthLogs.StoolConditionMaxLength.ToString());
        AssertElementHasAttribute(
            html,
            "Note",
            "maxlength",
            InputValidationLimits.HealthLogs.NoteMaxLength.ToString());
    }

    [Fact]
    public async Task VisitAndScheduleCreate_RenderConfiguredDateAndTextConstraints()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnerAndPet(dbContext);
            return Task.CompletedTask;
        });

        using var client = factory.CreateAuthenticatedClient("owner-user");

        using var visitResponse = await client.GetAsync("/Visits/Create?petId=1");
        Assert.Equal(HttpStatusCode.OK, visitResponse.StatusCode);
        var visitHtml = await visitResponse.Content.ReadAsStringAsync();
        AssertElementHasAttribute(visitHtml, "VisitDate", "type", "date");
        AssertElementHasAttribute(
            visitHtml,
            "ClinicName",
            "maxlength",
            InputValidationLimits.Visits.ClinicNameMaxLength.ToString());
        AssertElementHasAttribute(
            visitHtml,
            "Diagnosis",
            "maxlength",
            InputValidationLimits.Visits.DiagnosisMaxLength.ToString());
        AssertElementHasAttribute(
            visitHtml,
            "Prescription",
            "maxlength",
            InputValidationLimits.Visits.PrescriptionMaxLength.ToString());
        AssertElementHasAttribute(
            visitHtml,
            "Note",
            "maxlength",
            InputValidationLimits.Visits.NoteMaxLength.ToString());

        using var scheduleResponse = await client.GetAsync("/ScheduleItems/Create?petId=1");
        Assert.Equal(HttpStatusCode.OK, scheduleResponse.StatusCode);
        var scheduleHtml = await scheduleResponse.Content.ReadAsStringAsync();
        AssertElementHasAttribute(scheduleHtml, "DueDate", "type", "date");
        AssertElementHasAttribute(
            scheduleHtml,
            "Title",
            "maxlength",
            InputValidationLimits.ScheduleItems.TitleMaxLength.ToString());
        AssertElementHasAttribute(
            scheduleHtml,
            "Note",
            "maxlength",
            InputValidationLimits.ScheduleItems.NoteMaxLength.ToString());
    }

    private static void SeedOwnerAndPet(PetHealthManagement.Web.Data.ApplicationDbContext dbContext)
    {
        var now = new DateTimeOffset(2026, 3, 30, 9, 0, 0, TimeSpan.FromHours(9));

        dbContext.Users.Add(new ApplicationUser
        {
            Id = "owner-user",
            UserName = "owner-user",
            Email = "owner@example.com"
        });

        dbContext.Pets.Add(new Pet
        {
            Id = 1,
            OwnerId = "owner-user",
            Name = "Mugi",
            SpeciesCode = "DOG",
            IsPublic = false,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static void AssertElementHasAttribute(
        string html,
        string fieldName,
        string attributeName,
        string expectedAttributeValue)
    {
        var pattern = $@"<(input|textarea|select)\b[^>]*\bname=""{Regex.Escape(fieldName)}""[^>]*>";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);

        Assert.True(match.Success, $"Could not find an element for field '{fieldName}'.");
        Assert.Contains(
            $"{attributeName}=\"{expectedAttributeValue}\"",
            match.Value,
            StringComparison.OrdinalIgnoreCase);
    }
}

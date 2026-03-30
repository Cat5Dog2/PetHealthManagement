using System.ComponentModel.DataAnnotations;
using System.Reflection;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.ViewModels.Account;
using PetHealthManagement.Web.ViewModels.HealthLogs;
using PetHealthManagement.Web.ViewModels.Pets;
using PetHealthManagement.Web.ViewModels.ScheduleItems;
using PetHealthManagement.Web.ViewModels.Visits;

namespace PetHealthManagement.Web.Tests.ViewModels;

public class InputValidationMetadataTests
{
    [Fact]
    public void EditProfileViewModel_DisplayName_RejectsValuesOverConfiguredLength()
    {
        var model = new EditProfileViewModel
        {
            DisplayName = new string('a', InputValidationLimits.Profile.DisplayNameMaxLength + 1)
        };

        var validationResults = Validate(model);

        AssertHasValidationErrorFor(validationResults, nameof(EditProfileViewModel.DisplayName));
    }

    [Fact]
    public void PetEditViewModel_AppliesConfiguredStringLengthLimits()
    {
        var model = new PetEditViewModel
        {
            Name = new string('a', InputValidationLimits.Pets.NameMaxLength + 1),
            SpeciesCode = "DOG",
            Breed = new string('b', InputValidationLimits.Pets.BreedMaxLength + 1)
        };

        var validationResults = Validate(model);

        AssertHasValidationErrorFor(validationResults, nameof(PetEditViewModel.Name));
        AssertHasValidationErrorFor(validationResults, nameof(PetEditViewModel.Breed));
    }

    [Fact]
    public void HealthLogEditViewModel_AppliesConfiguredNumericAndTextLimits()
    {
        var model = new HealthLogEditViewModel
        {
            PetId = 1,
            PetName = "Mugi",
            RecordedAt = new DateTime(2026, 3, 30, 10, 0, 0),
            WeightKg = InputValidationLimits.HealthLogs.WeightKgMax + 0.1,
            FoodAmountGram = InputValidationLimits.HealthLogs.FoodAmountGramMax + 1,
            WalkMinutes = InputValidationLimits.HealthLogs.WalkMinutesMax + 1,
            StoolCondition = new string('s', InputValidationLimits.HealthLogs.StoolConditionMaxLength + 1),
            Note = new string('n', InputValidationLimits.HealthLogs.NoteMaxLength + 1)
        };

        var validationResults = Validate(model);

        AssertHasValidationErrorFor(validationResults, nameof(HealthLogEditViewModel.WeightKg));
        AssertHasValidationErrorFor(validationResults, nameof(HealthLogEditViewModel.FoodAmountGram));
        AssertHasValidationErrorFor(validationResults, nameof(HealthLogEditViewModel.WalkMinutes));
        AssertHasValidationErrorFor(validationResults, nameof(HealthLogEditViewModel.StoolCondition));
        AssertHasValidationErrorFor(validationResults, nameof(HealthLogEditViewModel.Note));
    }

    [Fact]
    public void VisitEditViewModel_AppliesConfiguredStringLengthLimits()
    {
        var model = new VisitEditViewModel
        {
            PetId = 1,
            PetName = "Mugi",
            VisitDate = new DateTime(2026, 3, 30),
            ClinicName = new string('c', InputValidationLimits.Visits.ClinicNameMaxLength + 1),
            Diagnosis = new string('d', InputValidationLimits.Visits.DiagnosisMaxLength + 1),
            Prescription = new string('p', InputValidationLimits.Visits.PrescriptionMaxLength + 1),
            Note = new string('n', InputValidationLimits.Visits.NoteMaxLength + 1)
        };

        var validationResults = Validate(model);

        AssertHasValidationErrorFor(validationResults, nameof(VisitEditViewModel.ClinicName));
        AssertHasValidationErrorFor(validationResults, nameof(VisitEditViewModel.Diagnosis));
        AssertHasValidationErrorFor(validationResults, nameof(VisitEditViewModel.Prescription));
        AssertHasValidationErrorFor(validationResults, nameof(VisitEditViewModel.Note));
    }

    [Fact]
    public void ScheduleItemEditViewModel_AppliesConfiguredStringLengthLimits()
    {
        var model = new ScheduleItemEditViewModel
        {
            PetId = 1,
            PetName = "Mugi",
            DueDate = new DateTime(2026, 3, 30),
            ItemType = new string('t', InputValidationLimits.ScheduleItems.ItemTypeMaxLength + 1),
            Title = new string('i', InputValidationLimits.ScheduleItems.TitleMaxLength + 1),
            Note = new string('n', InputValidationLimits.ScheduleItems.NoteMaxLength + 1)
        };

        var validationResults = Validate(model);

        AssertHasValidationErrorFor(validationResults, nameof(ScheduleItemEditViewModel.ItemType));
        AssertHasValidationErrorFor(validationResults, nameof(ScheduleItemEditViewModel.Title));
        AssertHasValidationErrorFor(validationResults, nameof(ScheduleItemEditViewModel.Note));
    }

    [Fact]
    public void DateFields_ExposeExpectedEditorMetadata()
    {
        AssertDateMetadata(
            typeof(HealthLogEditViewModel).GetProperty(nameof(HealthLogEditViewModel.RecordedAt))!,
            DataType.DateTime,
            InputValidationLimits.DateTimeLocalInputFormat);
        AssertDateMetadata(
            typeof(VisitEditViewModel).GetProperty(nameof(VisitEditViewModel.VisitDate))!,
            DataType.Date,
            InputValidationLimits.DateInputFormat);
        AssertDateMetadata(
            typeof(ScheduleItemEditViewModel).GetProperty(nameof(ScheduleItemEditViewModel.DueDate))!,
            DataType.Date,
            InputValidationLimits.DateInputFormat);
    }

    private static List<ValidationResult> Validate(object model)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), validationResults, validateAllProperties: true);
        return validationResults;
    }

    private static void AssertHasValidationErrorFor(
        IEnumerable<ValidationResult> validationResults,
        string memberName)
    {
        Assert.Contains(
            validationResults,
            result => result.MemberNames.Contains(memberName, StringComparer.Ordinal));
    }

    private static void AssertDateMetadata(PropertyInfo propertyInfo, DataType expectedDataType, string expectedFormat)
    {
        var dataTypeAttribute = propertyInfo.GetCustomAttribute<DataTypeAttribute>();
        var displayFormatAttribute = propertyInfo.GetCustomAttribute<DisplayFormatAttribute>();

        Assert.NotNull(dataTypeAttribute);
        Assert.Equal(expectedDataType, dataTypeAttribute!.DataType);
        Assert.NotNull(displayFormatAttribute);
        Assert.Equal("{0:" + expectedFormat + "}", displayFormatAttribute!.DataFormatString);
        Assert.True(displayFormatAttribute.ApplyFormatInEditMode);
    }
}

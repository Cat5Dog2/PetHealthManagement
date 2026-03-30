namespace PetHealthManagement.Web.Infrastructure;

public static class InputValidationLimits
{
    public const string DateInputFormat = "yyyy-MM-dd";
    public const string DateTimeLocalInputFormat = "yyyy-MM-ddTHH:mm";

    public static class Profile
    {
        public const int DisplayNameMaxLength = 50;
    }

    public static class Pets
    {
        public const int NameMaxLength = 50;
        public const int SpeciesCodeMaxLength = 50;
        public const int BreedMaxLength = 100;
    }

    public static class HealthLogs
    {
        public const double WeightKgMin = 0.0;
        public const double WeightKgMax = 200.0;
        public const double WeightKgStep = 0.1;
        public const int FoodAmountGramMin = 0;
        public const int FoodAmountGramMax = 5000;
        public const int WalkMinutesMin = 0;
        public const int WalkMinutesMax = 1440;
        public const int StoolConditionMaxLength = 50;
        public const int NoteMaxLength = 1000;
    }

    public static class Visits
    {
        public const int ClinicNameMaxLength = 100;
        public const int DiagnosisMaxLength = 500;
        public const int PrescriptionMaxLength = 500;
        public const int NoteMaxLength = 1000;
    }

    public static class ScheduleItems
    {
        public const int ItemTypeMaxLength = 20;
        public const int TitleMaxLength = 100;
        public const int NoteMaxLength = 1000;
    }
}

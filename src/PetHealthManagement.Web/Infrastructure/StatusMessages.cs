namespace PetHealthManagement.Web.Infrastructure;

public static class StatusMessages
{
    // Identity の管理画面が "StatusMessage" キーを使うため、衝突しない専用キーにする
    public const string TempDataKey = "AppStatusMessage";

    public const string PetCreated = "ペットを登録しました。";
    public const string PetUpdated = "ペット情報を更新しました。";
    public const string PetDeleted = "ペットを削除しました。";

    public const string HealthLogCreated = "健康ログを保存しました。";
    public const string HealthLogUpdated = "健康ログを更新しました。";
    public const string HealthLogDeleted = "健康ログを削除しました。";

    public const string ScheduleItemCreated = "予定を追加しました。";
    public const string ScheduleItemUpdated = "予定を更新しました。";
    public const string ScheduleItemDeleted = "予定を削除しました。";
    public const string ScheduleItemMarkedDone = "予定を完了にしました。";
    public const string ScheduleItemMarkedNotDone = "予定を未完了に戻しました。";

    public const string VisitCreated = "通院履歴を保存しました。";
    public const string VisitUpdated = "通院履歴を更新しました。";
    public const string VisitDeleted = "通院履歴を削除しました。";

    public const string ProfileUpdated = "プロフィールを更新しました。";
}

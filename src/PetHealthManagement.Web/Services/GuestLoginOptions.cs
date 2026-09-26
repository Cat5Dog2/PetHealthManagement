namespace PetHealthManagement.Web.Services;

public class GuestLoginOptions
{
    public const string SectionName = "GuestLogin";

    // false の場合、ゲストログインのボタンを表示せず、POST には 404 を返す
    public bool Enabled { get; set; }

    // 期限切れゲストの自動削除。ゲストログインを止めた後も、残ったゲストを消せるよう Enabled とは独立させる
    public bool CleanupEnabled { get; set; } = true;
}

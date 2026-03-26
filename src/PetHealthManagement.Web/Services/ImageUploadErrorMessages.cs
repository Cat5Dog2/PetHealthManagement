namespace PetHealthManagement.Web.Services;

public static class ImageUploadErrorMessages
{
    public const string UnsupportedFormat = "対応していない画像形式です（JPEG/PNG/WebP のみ）。";

    public const string FileTooLarge = "画像サイズが上限を超えています（1枚あたり最大2MB）。";

    public const string TooManyAttachments = "添付できる画像は最大10枚です（既存分を含む）。";

    public const string TotalStorageExceeded = "画像の合計容量が上限（100MB）を超えます。不要な画像を削除してください。";

    public const string DimensionsExceeded = "画像サイズが上限を超えています（最大辺4096px、総画素数16,777,216px以下）。";

    public const string SaveFailed = "画像の保存に失敗しました。時間をおいて再度お試しください。";
}

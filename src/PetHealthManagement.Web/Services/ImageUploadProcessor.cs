using SkiaSharp;

namespace PetHealthManagement.Web.Services;

// アップロード画像の実データ検証と正規化を一か所で行う。
// デコード → 向き補正 → 再エンコードし、EXIF などのメタデータを持たない画像だけを保存対象にする。
public static class ImageUploadProcessor
{
    private const long MaxFileSizeBytes = 2 * 1024 * 1024;
    private const int MaxEdgePixels = 4096;
    private const long MaxTotalPixels = 16_777_216;
    private const int JpegQuality = 90;
    private const int WebpQuality = 75;

    public static async Task<ImageUploadProcessingResult> ProcessAsync(
        string originalTempPath,
        string processedTempPath,
        CancellationToken cancellationToken)
    {
        var originalBytes = await File.ReadAllBytesAsync(originalTempPath, cancellationToken);
        using var originalData = SKData.CreateCopy(originalBytes);
        using var codec = SKCodec.Create(originalData);
        if (codec is null)
        {
            return ImageUploadProcessingResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }

        var mappedFormat = MapFormat(codec.EncodedFormat);
        if (mappedFormat is null)
        {
            return ImageUploadProcessingResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }

        var format = mappedFormat.Value;

        // 寸法はヘッダだけで判定し、ピクセルを展開する前に弾く。
        // 小さなファイルが巨大な画像に展開される（解凍爆弾）場合に、メモリを確保させないため。
        var encodedInfo = codec.Info;
        if (encodedInfo.Width > MaxEdgePixels || encodedInfo.Height > MaxEdgePixels)
        {
            return ImageUploadProcessingResult.Fail(ImageUploadErrorMessages.DimensionsExceeded);
        }

        var totalPixels = (long)encodedInfo.Width * encodedInfo.Height;
        if (totalPixels > MaxTotalPixels)
        {
            return ImageUploadProcessingResult.Fail(ImageUploadErrorMessages.DimensionsExceeded);
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var decoded = Decode(codec);
        if (decoded is null)
        {
            return ImageUploadProcessingResult.Fail(ImageUploadErrorMessages.UnsupportedFormat);
        }

        using var oriented = codec.EncodedOrigin == SKEncodedOrigin.TopLeft
            ? null
            : Orient(decoded, codec.EncodedOrigin);
        var output = oriented ?? decoded;

        cancellationToken.ThrowIfCancellationRequested();

        // 色空間のタグを外してから書き出す。ピクセルは Decode で sRGB に変換済みなので、
        // ICC プロファイルを埋め込まなくても表示は変わらない。
        using var untaggedPixmap = new SKPixmap(output.Info.WithColorSpace(null), output.GetPixels(), output.RowBytes);
        using var encoded = format.Encode(untaggedPixmap);
        if (encoded is null || encoded.Size <= 0)
        {
            return ImageUploadProcessingResult.Fail(ImageUploadErrorMessages.SaveFailed);
        }

        if (encoded.Size > MaxFileSizeBytes)
        {
            return ImageUploadProcessingResult.Fail(ImageUploadErrorMessages.FileTooLarge);
        }

        await File.WriteAllBytesAsync(processedTempPath, encoded.ToArray(), cancellationToken);

        return ImageUploadProcessingResult.Success(format.Extension, format.ContentType, encoded.Size);
    }

    private static (string Extension, string ContentType, Func<SKPixmap, SKData?> Encode)? MapFormat(
        SKEncodedImageFormat encodedFormat)
    {
        return encodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => (".jpg", "image/jpeg", pixmap => pixmap.Encode(
                new SKJpegEncoderOptions(JpegQuality, SKJpegEncoderDownsample.Downsample420, SKJpegEncoderAlphaOption.Ignore))),
            SKEncodedImageFormat.Png => (".png", "image/png", pixmap => pixmap.Encode(SKPngEncoderOptions.Default)),
            SKEncodedImageFormat.Webp => (".webp", "image/webp", pixmap => pixmap.Encode(
                new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, WebpQuality))),
            _ => null
        };
    }

    private static SKBitmap? Decode(SKCodec codec)
    {
        // 埋め込みの ICC プロファイル（iPhone の Display P3 など）は、ここで sRGB に変換する。
        var alphaType = codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul;
        var info = codec.Info
            .WithColorType(SKImageInfo.PlatformColorType)
            .WithAlphaType(alphaType)
            .WithColorSpace(SKColorSpace.CreateSrgb());

        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());

        // 途中で切れたデータ（IncompleteInput）も、破損・偽装として受け付けない。
        if (result != SKCodecResult.Success)
        {
            bitmap.Dispose();
            return null;
        }

        return bitmap;
    }

    // EXIF の Orientation をピクセルに反映する。書き出す画像は EXIF を持たないため、
    // ここで正規化しないと撮影時の向きが失われる。
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        float width = source.Width;
        float height = source.Height;

        // 変換後の座標 = (ScaleX * x + SkewX * y + TransX, SkewY * x + ScaleY * y + TransY)
        var (matrix, swapsAxes) = origin switch
        {
            SKEncodedOrigin.TopRight => (new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1), false),
            SKEncodedOrigin.BottomRight => (new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1), false),
            SKEncodedOrigin.BottomLeft => (new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1), false),
            SKEncodedOrigin.LeftTop => (new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1), true),
            SKEncodedOrigin.RightTop => (new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1), true),
            SKEncodedOrigin.RightBottom => (new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1), true),
            SKEncodedOrigin.LeftBottom => (new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1), true),
            _ => (SKMatrix.Identity, false)
        };

        var info = swapsAxes ? source.Info.WithSize(source.Height, source.Width) : source.Info;
        var oriented = new SKBitmap(info);
        using var canvas = new SKCanvas(oriented);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(matrix);
        // 90° 単位の回転と反転だけなので、補間せずピクセルをそのまま写す。
        canvas.DrawBitmap(source, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));

        return oriented;
    }
}

public sealed record ImageUploadProcessingResult(
    bool Succeeded,
    string? ErrorMessage,
    string? Extension,
    string? ContentType,
    long SizeBytes)
{
    public static ImageUploadProcessingResult Success(string extension, string contentType, long sizeBytes)
        => new(true, null, extension, contentType, sizeBytes);

    public static ImageUploadProcessingResult Fail(string errorMessage)
        => new(false, errorMessage, null, null, 0);
}

using System.Text;
using PetHealthManagement.Web.Services;
using PetHealthManagement.Web.Tests.Infrastructure;
using SkiaSharp;

namespace PetHealthManagement.Web.Tests.Services;

public class ImageUploadProcessorTests
{
    // 赤い左上の象限（幅・高さの半分）と青い背景。境界を 16px の倍数にそろえ、
    // JPEG の色差サブサンプリング（16x16 の MCU）で色が混ざらないようにしている。
    private const int SourceWidth = 64;
    private const int SourceHeight = 32;

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, ".jpg", "image/jpeg")]
    [InlineData(SKEncodedImageFormat.Png, ".png", "image/png")]
    [InlineData(SKEncodedImageFormat.Webp, ".webp", "image/webp")]
    public async Task ProcessAsync_ReencodesAllowedFormat_KeepingFormatAndDimensions(
        SKEncodedImageFormat format,
        string expectedExtension,
        string expectedContentType)
    {
        var (result, output) = await ProcessAsync(CreateQuadrantImage(format));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(expectedExtension, result.Extension);
        Assert.Equal(expectedContentType, result.ContentType);
        Assert.Equal(output.Length, result.SizeBytes);

        using var data = SKData.CreateCopy(output);
        using var codec = SKCodec.Create(data);
        Assert.NotNull(codec);
        Assert.Equal(format, codec.EncodedFormat);
        Assert.Equal(SourceWidth, codec.Info.Width);
        Assert.Equal(SourceHeight, codec.Info.Height);
    }

    [Theory]
    [InlineData(1, 64, 32, 8, 8)]
    [InlineData(2, 64, 32, 56, 8)]
    [InlineData(3, 64, 32, 56, 24)]
    [InlineData(4, 64, 32, 8, 24)]
    [InlineData(5, 32, 64, 8, 8)]
    [InlineData(6, 32, 64, 24, 8)]
    [InlineData(7, 32, 64, 24, 56)]
    [InlineData(8, 32, 64, 8, 56)]
    public async Task ProcessAsync_AppliesExifOrientation_AndDropsExif(
        ushort orientation,
        int expectedWidth,
        int expectedHeight,
        int redX,
        int redY)
    {
        var input = InsertExifOrientation(CreateQuadrantImage(SKEncodedImageFormat.Jpeg), orientation);
        Assert.True(ContainsAscii(input, "Exif\0\0"));

        var (result, output) = await ProcessAsync(input);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(ContainsAscii(output, "Exif\0\0"));

        using var bitmap = SKBitmap.Decode(output);
        Assert.Equal(expectedWidth, bitmap.Width);
        Assert.Equal(expectedHeight, bitmap.Height);
        AssertRed(bitmap.GetPixel(redX, redY));
        AssertBlue(bitmap.GetPixel(expectedWidth - 1 - redX, expectedHeight - 1 - redY));
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "ICC_PROFILE")]
    [InlineData(SKEncodedImageFormat.Png, "iCCP")]
    [InlineData(SKEncodedImageFormat.Webp, "ICCP")]
    public async Task ProcessAsync_ConvertsEmbeddedColorProfile_WithoutEmbeddingOne(
        SKEncodedImageFormat format,
        string iccMarker)
    {
        using var displayP3 = SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Srgb, SKColorSpaceXyz.DisplayP3);
        var input = CreateQuadrantImage(format, displayP3);
        Assert.True(ContainsAscii(input, iccMarker));

        var (result, output) = await ProcessAsync(input);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(ContainsAscii(output, iccMarker));
    }

    [Fact]
    public async Task ProcessAsync_RejectsOversizedImage_FromHeaderWithoutDecodingPixels()
    {
        // ヘッダは 4097px 幅のまま、画素データの末尾を切り落とす。
        // 先にデコードしていれば「壊れたデータ」になるため、寸法エラーになることでヘッダ段階の判定を確かめる。
        var png = CreateSolidPng(width: 4097, height: 1);
        var truncated = png[..^20];

        var (result, output) = await ProcessAsync(truncated);

        Assert.False(result.Succeeded);
        Assert.Equal(ImageUploadErrorMessages.DimensionsExceeded, result.ErrorMessage);
        Assert.Empty(output);
    }

    [Fact]
    public async Task ProcessAsync_ConvertsWideGamutColorsToSrgb()
    {
        // Display P3 のビットマップに sRGB の色を描くと、P3 の値に変換されて保存される。
        // sRGB に戻さずに書き出すと、表示される色がずれる。
        var expected = new SKColor(200, 100, 50);
        using var displayP3 = SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Srgb, SKColorSpaceXyz.DisplayP3);
        var input = CreateSolidPng(SourceWidth, SourceHeight, expected, displayP3);
        using (var rawInput = DecodeWithoutColorConversion(input))
        {
            Assert.True(MaxChannelDifference(rawInput.GetPixel(0, 0), expected) > 5);
        }

        var (result, output) = await ProcessAsync(input);

        Assert.True(result.Succeeded, result.ErrorMessage);
        using var bitmap = SKBitmap.Decode(output);
        Assert.InRange(MaxChannelDifference(bitmap.GetPixel(0, 0), expected), 0, 2);
    }

    [Fact]
    public async Task ProcessAsync_RejectsTruncatedImageData()
    {
        // ヘッダ（量子化・ハフマン表）は残し、SOS 以降の画素データの途中で切る。
        var jpeg = CreateQuadrantImage(SKEncodedImageFormat.Jpeg);
        byte[] startOfScanMarker = [0xFF, 0xDA];
        var startOfScan = jpeg.AsSpan().IndexOf(startOfScanMarker);
        Assert.True(startOfScan > 0);
        var truncated = jpeg[..(startOfScan + (jpeg.Length - startOfScan) / 2)];

        var (result, output) = await ProcessAsync(truncated);

        Assert.False(result.Succeeded);
        Assert.Equal(ImageUploadErrorMessages.UnsupportedFormat, result.ErrorMessage);
        Assert.Empty(output);
    }

    [Theory]
    [MemberData(nameof(UnsupportedData))]
    public async Task ProcessAsync_RejectsDataOutsideTheAllowedFormats(byte[] input)
    {
        var (result, output) = await ProcessAsync(input);

        Assert.False(result.Succeeded);
        Assert.Equal(ImageUploadErrorMessages.UnsupportedFormat, result.ErrorMessage);
        Assert.Empty(output);
    }

    public static TheoryData<byte[]> UnsupportedData => new()
    {
        Encoding.UTF8.GetBytes("this is not an image"),
        // 1x1 の GIF。SkiaSharp はデコードできるが、許可形式（JPEG/PNG/WebP）ではない。
        Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAICRAEAOw=="),
    };

    private static async Task<(ImageUploadProcessingResult Result, byte[] Output)> ProcessAsync(byte[] input)
    {
        using var root = new TemporaryStorageRoot("image-upload-processor-tests");
        var originalPath = Path.Combine(root.RootPath, "original");
        var processedPath = Path.Combine(root.RootPath, "processed");
        await File.WriteAllBytesAsync(originalPath, input);

        var result = await ImageUploadProcessor.ProcessAsync(originalPath, processedPath, CancellationToken.None);

        var output = File.Exists(processedPath) ? await File.ReadAllBytesAsync(processedPath) : [];
        return (result, output);
    }

    private static byte[] CreateQuadrantImage(SKEncodedImageFormat format, SKColorSpace? colorSpace = null)
    {
        var info = new SKImageInfo(SourceWidth, SourceHeight, SKColorType.Rgba8888, SKAlphaType.Premul, colorSpace);
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, SourceWidth / 2f, SourceHeight / 2f, paint);
        }

        using var data = bitmap.Encode(format, 95);
        return data.ToArray();
    }

    private static byte[] CreateSolidPng(int width, int height)
        => CreateSolidPng(width, height, SKColors.CadetBlue, colorSpace: null);

    private static byte[] CreateSolidPng(int width, int height, SKColor color, SKColorSpace? colorSpace)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul, colorSpace);
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(color);
        }

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // 保存されている値をそのまま読む（埋め込みの ICC プロファイルで変換しない）。
    private static SKBitmap DecodeWithoutColorConversion(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        Assert.NotNull(codec);
        var info = codec.Info.WithColorType(SKColorType.Rgba8888).WithColorSpace(null);
        var bitmap = new SKBitmap(info);
        Assert.Equal(SKCodecResult.Success, codec.GetPixels(info, bitmap.GetPixels()));
        return bitmap;
    }

    private static int MaxChannelDifference(SKColor actual, SKColor expected)
        => new[]
        {
            Math.Abs(actual.Red - expected.Red),
            Math.Abs(actual.Green - expected.Green),
            Math.Abs(actual.Blue - expected.Blue)
        }.Max();

    // SOI の直後に、Orientation だけを持つ EXIF（APP1）セグメントを差し込む。
    private static byte[] InsertExifOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] tiff =
        [
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, // little-endian TIFF ヘッダ、IFD0 は 8 バイト目
            0x01, 0x00,                                                 // エントリ数 1
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00,             // tag 0x0112 (Orientation), SHORT, count 1
            (byte)orientation, (byte)(orientation >> 8), 0x00, 0x00,    // 値
            0x00, 0x00, 0x00, 0x00                                      // 次の IFD なし
        ];
        var payload = Encoding.ASCII.GetBytes("Exif\0\0").Concat(tiff).ToArray();
        var segmentLength = payload.Length + 2;
        byte[] segmentHeader = [0xFF, 0xE1, (byte)(segmentLength >> 8), (byte)segmentLength];

        return [.. jpeg[..2], .. segmentHeader, .. payload, .. jpeg[2..]];
    }

    private static bool ContainsAscii(byte[] bytes, string marker)
        => bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(marker)) >= 0;

    private static void AssertRed(SKColor color)
        => Assert.True(color.Red > 200 && color.Blue < 60, $"Expected red but was {color}.");

    private static void AssertBlue(SKColor color)
        => Assert.True(color.Blue > 200 && color.Red < 60, $"Expected blue but was {color}.");
}

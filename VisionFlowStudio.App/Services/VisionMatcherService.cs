using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Rect = System.Drawing.Rectangle;

namespace VisionFlowStudio.App.Services;

public sealed class VisionMatcherService
{
    private readonly ScreenCaptureService _screenCaptureService;

    public VisionMatcherService(ScreenCaptureService screenCaptureService)
    {
        _screenCaptureService = screenCaptureService;
    }

    public Task<VisionMatchResult?> FindOnScreenAsync(string templateBase64, double threshold, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var templateBitmap = BitmapConversionHelper.ToBitmap(templateBase64);
        var capture = _screenCaptureService.CaptureVirtualScreen();
        using var screenshot = capture.Bitmap;
        return Task.FromResult(FindInBitmap(screenshot, capture.Bounds, templateBitmap, threshold));
    }

    public bool PixelMatches(System.Windows.Point screenPoint, string expectedColorHex, int tolerance)
    {
        var capture = _screenCaptureService.CaptureVirtualScreen();
        using var screenshot = capture.Bitmap;
        var sampleX = (int)Math.Round(screenPoint.X) - capture.Bounds.Left;
        var sampleY = (int)Math.Round(screenPoint.Y) - capture.Bounds.Top;
        if (sampleX < 0 || sampleY < 0 || sampleX >= screenshot.Width || sampleY >= screenshot.Height)
        {
            return false;
        }

        var pixel = screenshot.GetPixel(sampleX, sampleY);
        var expected = ColorTranslator.FromHtml(expectedColorHex);
        return Math.Abs(pixel.R - expected.R) <= tolerance
               && Math.Abs(pixel.G - expected.G) <= tolerance
               && Math.Abs(pixel.B - expected.B) <= tolerance;
    }

    private static VisionMatchResult? FindInBitmap(Bitmap sourceBitmap, Rect sourceBounds, Bitmap templateBitmap, double threshold)
    {
        using var sourceMat = BitmapConverter.ToMat(sourceBitmap);
        using var templateMat = BitmapConverter.ToMat(templateBitmap);
        using var sourceBgr = sourceMat.Channels() == 4 ? sourceMat.CvtColor(ColorConversionCodes.BGRA2BGR) : sourceMat.Clone();
        using var templateBgr = templateMat.Channels() == 4 ? templateMat.CvtColor(ColorConversionCodes.BGRA2BGR) : templateMat.Clone();
        using var result = new Mat();

        Cv2.MatchTemplate(sourceBgr, templateBgr, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxValue, out _, out var maxLocation);

        if (maxValue < threshold)
        {
            return null;
        }

        var absoluteBounds = new System.Windows.Rect(
            sourceBounds.Left + maxLocation.X,
            sourceBounds.Top + maxLocation.Y,
            templateBitmap.Width,
            templateBitmap.Height);

        return new VisionMatchResult(absoluteBounds, maxValue);
    }
}

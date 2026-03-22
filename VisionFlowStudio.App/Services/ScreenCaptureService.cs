using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Media.Imaging;

namespace VisionFlowStudio.App.Services;

public sealed class ScreenCaptureService
{
    public async Task<BitmapSource?> CaptureUserRegionAsync(CancellationToken cancellationToken = default)
    {
        var screenshot = await CaptureWithMainWindowHiddenAsync(cancellationToken);
        if (screenshot is null)
        {
            return null;
        }

        using var bitmap = screenshot.Value.Bitmap;
        var window = new SnippingOverlayWindow(new Bitmap(bitmap), screenshot.Value.Bounds);
        var accepted = window.ShowDialog();
        if (accepted != true || window.ResultBitmap is null)
        {
            return null;
        }

        using var resultBitmap = window.ResultBitmap;
        return BitmapConversionHelper.ToBitmapSource(resultBitmap);
    }

    public async Task<PointSelectionResult?> PickPointAsync(CancellationToken cancellationToken = default)
    {
        var screenshot = await CaptureWithMainWindowHiddenAsync(cancellationToken);
        if (screenshot is null)
        {
            return null;
        }

        using var bitmap = screenshot.Value.Bitmap;
        var window = new CrosshairPickerWindow(new Bitmap(bitmap), screenshot.Value.Bounds);
        return window.ShowDialog() == true ? window.Result : null;
    }

    public BitmapSource? GetClipboardImage() => BitmapConversionHelper.FromClipboard();

    public string EncodeToBase64(BitmapSource bitmapSource) => BitmapConversionHelper.ToPngBase64(bitmapSource);

    public (Bitmap Bitmap, Rectangle Bounds) CaptureVirtualScreen()
    {
        var bounds = VirtualScreenHelper.GetBounds();
        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        return (bitmap, bounds);
    }

    private async Task<(Bitmap Bitmap, Rectangle Bounds)?> CaptureWithMainWindowHiddenAsync(CancellationToken cancellationToken)
    {
        WindowState? previousState = null;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (Application.Current.MainWindow is not Window window)
            {
                return;
            }

            previousState = window.WindowState;
            window.WindowState = WindowState.Minimized;
        });

        try
        {
            await Task.Delay(180, cancellationToken);
            return CaptureVirtualScreen();
        }
        finally
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (Application.Current.MainWindow is not Window window || previousState is null)
                {
                    return;
                }

                window.WindowState = previousState.Value;
                window.Activate();
            });
        }
    }
}


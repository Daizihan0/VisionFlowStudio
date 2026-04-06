using System.Windows;
using System.Windows.Media;

namespace VisionFlowStudio.App.Services;

public static class OverlayDpiHelper
{
    public static DpiScale GetDpiScale()
    {
        if (Application.Current?.MainWindow is Window mainWindow)
        {
            return VisualTreeHelper.GetDpi(mainWindow);
        }

        return new DpiScale(1.0, 1.0);
    }

    public static double PixelsToDipX(double pixels, DpiScale dpiScale) => pixels / dpiScale.DpiScaleX;

    public static double PixelsToDipY(double pixels, DpiScale dpiScale) => pixels / dpiScale.DpiScaleY;

    public static double DipToPixelsX(double dips, DpiScale dpiScale) => dips * dpiScale.DpiScaleX;

    public static double DipToPixelsY(double dips, DpiScale dpiScale) => dips * dpiScale.DpiScaleY;
}

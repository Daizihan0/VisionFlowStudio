using System.Drawing;
using System.Runtime.InteropServices;

namespace VisionFlowStudio.App.Services;

public static class VirtualScreenHelper
{
    private const int SmXvirtualscreen = 76;
    private const int SmYvirtualscreen = 77;
    private const int SmCxvirtualscreen = 78;
    private const int SmCyvirtualscreen = 79;

    public static Rectangle GetBounds() => new(
        GetSystemMetrics(SmXvirtualscreen),
        GetSystemMetrics(SmYvirtualscreen),
        GetSystemMetrics(SmCxvirtualscreen),
        GetSystemMetrics(SmCyvirtualscreen));

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}

namespace VisionFlowStudio.App.Services;

public sealed record VisionMatchResult(System.Windows.Rect Bounds, double Score)
{
    public System.Windows.Point Center => new(Bounds.X + (Bounds.Width / 2), Bounds.Y + (Bounds.Height / 2));
}

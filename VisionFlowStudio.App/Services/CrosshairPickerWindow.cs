using DrawingBitmap = System.Drawing.Bitmap;
using DrawingRect = System.Drawing.Rectangle;
using WpfPoint = System.Windows.Point;
using MediaColor = System.Windows.Media.Color;
using ShapeRectangle = System.Windows.Shapes.Rectangle;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VisionFlowStudio.App.Services;

public sealed class CrosshairPickerWindow : Window
{
    private readonly DrawingRect _virtualBounds;
    private readonly DrawingBitmap _screenshot;
    private readonly Canvas _canvas;
    private readonly Line _horizontalLine;
    private readonly Line _verticalLine;
    private readonly Border _tooltip;
    private readonly TextBlock _tooltipText;
    private readonly DpiScale _dpiScale;
    private readonly double _surfaceWidth;
    private readonly double _surfaceHeight;

    public CrosshairPickerWindow(DrawingBitmap screenshot, DrawingRect virtualBounds)
    {
        _screenshot = screenshot;
        _virtualBounds = virtualBounds;
        _dpiScale = OverlayDpiHelper.GetDpiScale();
        _surfaceWidth = OverlayDpiHelper.PixelsToDipX(virtualBounds.Width, _dpiScale);
        _surfaceHeight = OverlayDpiHelper.PixelsToDipY(virtualBounds.Height, _dpiScale);

        Left = OverlayDpiHelper.PixelsToDipX(virtualBounds.Left, _dpiScale);
        Top = OverlayDpiHelper.PixelsToDipY(virtualBounds.Top, _dpiScale);
        Width = _surfaceWidth;
        Height = _surfaceHeight;
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;

        _canvas = new Canvas
        {
            Width = _surfaceWidth,
            Height = _surfaceHeight
        };
        Content = _canvas;

        var background = new Image
        {
            Source = BitmapConversionHelper.ToBitmapSource(_screenshot),
            Stretch = Stretch.Fill,
            Width = _surfaceWidth,
            Height = _surfaceHeight,
            Opacity = 0.95
        };

        _canvas.Children.Add(background);

        var darkOverlay = new ShapeRectangle
        {
            Width = _surfaceWidth,
            Height = _surfaceHeight,
            Fill = new SolidColorBrush(MediaColor.FromArgb(55, 10, 15, 25))
        };
        _canvas.Children.Add(darkOverlay);

        _horizontalLine = new Line
        {
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = 1.2,
            X1 = 0,
            X2 = _surfaceWidth,
            Y1 = 0,
            Y2 = 0
        };
        _verticalLine = new Line
        {
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = 1.2,
            X1 = 0,
            X2 = 0,
            Y1 = 0,
            Y2 = _surfaceHeight
        };
        _canvas.Children.Add(_horizontalLine);
        _canvas.Children.Add(_verticalLine);

        _tooltipText = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        };
        _tooltip = new Border
        {
            Background = new SolidColorBrush(MediaColor.FromArgb(220, 15, 23, 42)),
            BorderBrush = Brushes.DeepSkyBlue,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 6, 10, 6),
            Child = _tooltipText
        };
        _canvas.Children.Add(_tooltip);

        MouseMove += OnMouseMove;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public PointSelectionResult? Result { get; private set; }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var point = e.GetPosition(this);
        UpdateCrosshair(point);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(this);
        var absoluteX = _virtualBounds.Left + (int)Math.Round(OverlayDpiHelper.DipToPixelsX(point.X, _dpiScale));
        var absoluteY = _virtualBounds.Top + (int)Math.Round(OverlayDpiHelper.DipToPixelsY(point.Y, _dpiScale));
        var safeX = Math.Clamp(absoluteX - _virtualBounds.Left, 0, _screenshot.Width - 1);
        var safeY = Math.Clamp(absoluteY - _virtualBounds.Top, 0, _screenshot.Height - 1);
        var color = _screenshot.GetPixel(safeX, safeY);

        Result = new PointSelectionResult(
            new WpfPoint(absoluteX, absoluteY),
            $"#{color.R:X2}{color.G:X2}{color.B:X2}");

        DialogResult = true;
        Close();
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        DialogResult = false;
        Close();
    }

    private void UpdateCrosshair(WpfPoint point)
    {
        _horizontalLine.Y1 = point.Y;
        _horizontalLine.Y2 = point.Y;
        _verticalLine.X1 = point.X;
        _verticalLine.X2 = point.X;

        var absoluteX = _virtualBounds.Left + (int)Math.Round(OverlayDpiHelper.DipToPixelsX(point.X, _dpiScale));
        var absoluteY = _virtualBounds.Top + (int)Math.Round(OverlayDpiHelper.DipToPixelsY(point.Y, _dpiScale));
        var safeX = Math.Clamp(absoluteX - _virtualBounds.Left, 0, _screenshot.Width - 1);
        var safeY = Math.Clamp(absoluteY - _virtualBounds.Top, 0, _screenshot.Height - 1);
        var color = _screenshot.GetPixel(safeX, safeY);

        _tooltipText.Text = $"X:{absoluteX}  Y:{absoluteY}  色值 #{color.R:X2}{color.G:X2}{color.B:X2}";
        Canvas.SetLeft(_tooltip, Math.Min(point.X + 18, Math.Max(0, _surfaceWidth - 220)));
        Canvas.SetTop(_tooltip, Math.Min(point.Y + 18, Math.Max(0, _surfaceHeight - 48)));
    }
}

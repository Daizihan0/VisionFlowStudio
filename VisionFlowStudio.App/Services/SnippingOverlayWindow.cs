using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingGraphicsUnit = System.Drawing.GraphicsUnit;
using DrawingRect = System.Drawing.Rectangle;
using WpfPoint = System.Windows.Point;
using MediaColor = System.Windows.Media.Color;
using ShapeRectangle = System.Windows.Shapes.Rectangle;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VisionFlowStudio.App.Services;

public sealed class SnippingOverlayWindow : Window
{
    private readonly DrawingRect _virtualBounds;
    private readonly DrawingBitmap _screenshot;
    private readonly Canvas _canvas;
    private readonly ShapeRectangle _selectionRectangle;
    private readonly Border _tip;
    private readonly TextBlock _tipText;
    private readonly DpiScale _dpiScale;
    private readonly double _surfaceWidth;
    private readonly double _surfaceHeight;
    private WpfPoint _selectionStart;
    private bool _isDragging;

    public SnippingOverlayWindow(DrawingBitmap screenshot, DrawingRect virtualBounds)
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

        _canvas.Children.Add(new Image
        {
            Source = BitmapConversionHelper.ToBitmapSource(_screenshot),
            Stretch = Stretch.Fill,
            Width = _surfaceWidth,
            Height = _surfaceHeight,
            Opacity = 0.96
        });

        _canvas.Children.Add(new ShapeRectangle
        {
            Width = _surfaceWidth,
            Height = _surfaceHeight,
            Fill = new SolidColorBrush(MediaColor.FromArgb(68, 5, 10, 20))
        });

        _selectionRectangle = new ShapeRectangle
        {
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(MediaColor.FromArgb(50, 56, 189, 248)),
            Visibility = Visibility.Collapsed
        };
        _canvas.Children.Add(_selectionRectangle);

        _tipText = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        };
        _tip = new Border
        {
            Background = new SolidColorBrush(MediaColor.FromArgb(220, 15, 23, 42)),
            BorderBrush = Brushes.DeepSkyBlue,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 6, 10, 6),
            Child = _tipText
        };
        _canvas.Children.Add(_tip);

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public DrawingBitmap? ResultBitmap { get; private set; }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _selectionStart = e.GetPosition(this);
        _isDragging = true;
        _selectionRectangle.Visibility = Visibility.Visible;
        CaptureMouse();
        UpdateSelection(e.GetPosition(this));
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var point = e.GetPosition(this);
        if (_isDragging)
        {
            UpdateSelection(point);
        }
        else
        {
            UpdateTip(point, null);
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ReleaseMouseCapture();

        var end = e.GetPosition(this);
        var rect = CreateRect(_selectionStart, end);
        if (rect.Width < 4 || rect.Height < 4)
        {
            _selectionRectangle.Visibility = Visibility.Collapsed;
            return;
        }

        ResultBitmap = Crop(rect);
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

    private void UpdateSelection(WpfPoint current)
    {
        var rect = CreateRect(_selectionStart, current);
        Canvas.SetLeft(_selectionRectangle, rect.X);
        Canvas.SetTop(_selectionRectangle, rect.Y);
        _selectionRectangle.Width = rect.Width;
        _selectionRectangle.Height = rect.Height;
        UpdateTip(current, rect);
    }

    private void UpdateTip(WpfPoint point, System.Windows.Rect? selectionRect)
    {
        _tipText.Text = selectionRect is { } rect
            ? $"宽 {Math.Round(OverlayDpiHelper.DipToPixelsX(rect.Width, _dpiScale))} × 高 {Math.Round(OverlayDpiHelper.DipToPixelsY(rect.Height, _dpiScale))}"
            : "拖动鼠标框选区域，Esc 取消";

        Canvas.SetLeft(_tip, Math.Min(point.X + 16, Math.Max(0, _surfaceWidth - 220)));
        Canvas.SetTop(_tip, Math.Min(point.Y + 16, Math.Max(0, _surfaceHeight - 48)));
    }

    private DrawingBitmap Crop(System.Windows.Rect rect)
    {
        var sourceRect = new DrawingRect(
            x: (int)Math.Round(OverlayDpiHelper.DipToPixelsX(rect.X, _dpiScale)),
            y: (int)Math.Round(OverlayDpiHelper.DipToPixelsY(rect.Y, _dpiScale)),
            width: (int)Math.Round(OverlayDpiHelper.DipToPixelsX(rect.Width, _dpiScale)),
            height: (int)Math.Round(OverlayDpiHelper.DipToPixelsY(rect.Height, _dpiScale)));

        sourceRect.X = Math.Clamp(sourceRect.X, 0, _screenshot.Width - 1);
        sourceRect.Y = Math.Clamp(sourceRect.Y, 0, _screenshot.Height - 1);
        sourceRect.Width = Math.Clamp(sourceRect.Width, 1, _screenshot.Width - sourceRect.X);
        sourceRect.Height = Math.Clamp(sourceRect.Height, 1, _screenshot.Height - sourceRect.Y);

        var target = new DrawingBitmap(sourceRect.Width, sourceRect.Height);
        using var graphics = DrawingGraphics.FromImage(target);
        graphics.DrawImage(_screenshot, new DrawingRect(0, 0, target.Width, target.Height), sourceRect, DrawingGraphicsUnit.Pixel);
        return target;
    }

    private static System.Windows.Rect CreateRect(WpfPoint start, WpfPoint end)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        return new System.Windows.Rect(x, y, width, height);
    }
}

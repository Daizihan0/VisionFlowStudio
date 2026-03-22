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
    private WpfPoint _selectionStart;
    private bool _isDragging;

    public SnippingOverlayWindow(DrawingBitmap screenshot, DrawingRect virtualBounds)
    {
        _screenshot = screenshot;
        _virtualBounds = virtualBounds;

        Left = virtualBounds.Left;
        Top = virtualBounds.Top;
        Width = virtualBounds.Width;
        Height = virtualBounds.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;

        _canvas = new Canvas();
        Content = _canvas;

        _canvas.Children.Add(new System.Windows.Controls.Image
        {
            Source = BitmapConversionHelper.ToBitmapSource(_screenshot),
            Stretch = Stretch.Fill,
            Width = virtualBounds.Width,
            Height = virtualBounds.Height,
            Opacity = 0.96
        });

        _canvas.Children.Add(new ShapeRectangle
        {
            Width = virtualBounds.Width,
            Height = virtualBounds.Height,
            Fill = new SolidColorBrush(MediaColor.FromArgb(68, 5, 10, 20))
        });

        _selectionRectangle = new ShapeRectangle
        {
            Stroke = System.Windows.Media.Brushes.DeepSkyBlue,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(MediaColor.FromArgb(50, 56, 189, 248)),
            Visibility = Visibility.Collapsed
        };
        _canvas.Children.Add(_selectionRectangle);

        _tipText = new TextBlock
        {
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        };
        _tip = new Border
        {
            Background = new SolidColorBrush(MediaColor.FromArgb(220, 15, 23, 42)),
            BorderBrush = System.Windows.Media.Brushes.DeepSkyBlue,
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
            ? $"宽 {Math.Round(rect.Width)} × 高 {Math.Round(rect.Height)}"
            : "拖动鼠标框选区域，Esc 取消";

        Canvas.SetLeft(_tip, Math.Min(point.X + 16, Math.Max(0, ActualWidth - 220)));
        Canvas.SetTop(_tip, Math.Min(point.Y + 16, Math.Max(0, ActualHeight - 48)));
    }

    private DrawingBitmap Crop(System.Windows.Rect rect)
    {
        var sourceRect = new DrawingRect(
            x: (int)Math.Round(rect.X),
            y: (int)Math.Round(rect.Y),
            width: (int)Math.Round(rect.Width),
            height: (int)Math.Round(rect.Height));

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

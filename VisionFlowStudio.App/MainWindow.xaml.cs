using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Media.Animation;
using VisionFlowStudio.App.Services;
using VisionFlowStudio.App.ViewModels;
using VisionFlowStudio.Infrastructure.Services;

namespace VisionFlowStudio.App;

public partial class MainWindow : Window
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyRecordStopId = 1001;
    private const int HotkeyEmergencyStopId = 1002;

    private FlowNodeViewModel? _draggingNode;
    private FrameworkElement? _dragSurface;
    private Point _dragOriginPoint;
    private double _nodeOriginX;
    private double _nodeOriginY;
    private HwndSource? _hwndSource;
    private bool _isCanvasPanning;
    private Point _canvasPanOrigin;
    private double _canvasPanHorizontalOffset;
    private double _canvasPanVerticalOffset;
    private ScrollViewer? _canvasPanScrollViewer;
    private bool _isCanvasFullscreen;
    private WindowStyle _previousWindowStyle;
    private WindowState _previousWindowState;
    private ResizeMode _previousResizeMode;
    private bool _previousTopmost;

    public MainWindow()
    {
        InitializeComponent();

        var screenCaptureService = new ScreenCaptureService();
        var visionMatcherService = new VisionMatcherService(screenCaptureService);
        var inputSimulationService = new InputSimulationService();
        var inputRecordingService = new InputRecordingService();

        DataContext = new MainViewModel(
            new JsonProjectStorageService(),
            new JsonNodeTemplateLibraryService(),
            new PreviewFlowExecutionEngine(),
            new DesktopFlowExecutionEngine(visionMatcherService, inputSimulationService),
            screenCaptureService,
            inputRecordingService);

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        _hwndSource?.AddHook(WndProc);

        var handle = new WindowInteropHelper(this).Handle;
        RegisterHotKey(handle, HotkeyRecordStopId, 0, (uint)KeyInterop.VirtualKeyFromKey(Key.F9));
        RegisterHotKey(handle, HotkeyEmergencyStopId, 0, (uint)KeyInterop.VirtualKeyFromKey(Key.F10));
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(handle, HotkeyRecordStopId);
        UnregisterHotKey(handle, HotkeyEmergencyStopId);
        _hwndSource?.RemoveHook(WndProc);

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && DataContext is MainViewModel viewModel)
        {
            _ = viewModel.HandleHotkeyAsync(wParam.ToInt32());
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.HasSelectedNode) or nameof(MainViewModel.SelectedNode))
        {
            Dispatcher.Invoke(() =>
            {
                if (sender is MainViewModel viewModel)
                {
                    UpdateFullscreenNodeDrawer(viewModel.HasSelectedNode, true);
                }
            });
        }
    }

    private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FlowNodeViewModel nodeViewModel })
        {
            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            var handledByConnectionMode = viewModel.TryHandleNodeClick(nodeViewModel);
            if (handledByConnectionMode)
            {
                e.Handled = true;
                return;
            }
        }

        _draggingNode = nodeViewModel;
        _dragSurface = FindAncestor<Canvas>(sender as DependencyObject) as FrameworkElement;
        _dragOriginPoint = e.GetPosition(_dragSurface ?? (IInputElement)sender);
        _nodeOriginX = nodeViewModel.X;
        _nodeOriginY = nodeViewModel.Y;
        nodeViewModel.IsSelected = true;
        Mouse.Capture((IInputElement)sender);
        e.Handled = true;
    }

    private void Node_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_draggingNode is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPosition = e.GetPosition(_dragSurface ?? (IInputElement)sender);
        var delta = currentPosition - _dragOriginPoint;

        _draggingNode.X = Math.Max(24, _nodeOriginX + delta.X);
        _draggingNode.Y = Math.Max(24, _nodeOriginY + delta.Y);
    }

    private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingNode is null)
        {
            return;
        }

        Mouse.Capture(null);
        _draggingNode = null;
        _dragSurface = null;
        e.Handled = true;
    }

    private void DesignerScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.AdjustZoom(e.Delta > 0 ? 0.1d : -0.1d);
        e.Handled = true;
    }

    private void Connection_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DesignerConnectionViewModel connectionViewModel }
            || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.SelectConnection(connectionViewModel);
        e.Handled = true;
    }

    private void DesignerCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement surface)
        {
            return;
        }

        var scrollViewer = FindAncestor<ScrollViewer>(surface);
        if (scrollViewer is null)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel && _isCanvasFullscreen)
        {
            viewModel.ClearSelection();
        }

        _isCanvasPanning = true;
        _canvasPanScrollViewer = scrollViewer;
        _canvasPanOrigin = e.GetPosition(scrollViewer);
        _canvasPanHorizontalOffset = scrollViewer.HorizontalOffset;
        _canvasPanVerticalOffset = scrollViewer.VerticalOffset;
        surface.Cursor = Cursors.Hand;
        Mouse.Capture(surface);
        e.Handled = true;
    }

    private void DesignerCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isCanvasPanning || e.LeftButton != MouseButtonState.Pressed || _canvasPanScrollViewer is null)
        {
            return;
        }

        var current = e.GetPosition(_canvasPanScrollViewer);
        var delta = current - _canvasPanOrigin;
        _canvasPanScrollViewer.ScrollToHorizontalOffset(Math.Max(0d, _canvasPanHorizontalOffset - delta.X));
        _canvasPanScrollViewer.ScrollToVerticalOffset(Math.Max(0d, _canvasPanVerticalOffset - delta.Y));
        e.Handled = true;
    }

    private void DesignerCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndCanvasPan(sender as FrameworkElement);
        e.Handled = true;
    }

    private void CanvasRightResizeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var delta = e.HorizontalChange / Math.Max(viewModel.ZoomScale, 0.1d);
        viewModel.ResizeDesignerCanvas(viewModel.DesignerCanvasWidth + delta, null);
        e.Handled = true;
    }

    private void CanvasBottomResizeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var delta = e.VerticalChange / Math.Max(viewModel.ZoomScale, 0.1d);
        viewModel.ResizeDesignerCanvas(null, viewModel.DesignerCanvasHeight + delta);
        e.Handled = true;
    }

    private void CanvasCornerResizeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var scale = Math.Max(viewModel.ZoomScale, 0.1d);
        viewModel.ResizeDesignerCanvas(
            viewModel.DesignerCanvasWidth + (e.HorizontalChange / scale),
            viewModel.DesignerCanvasHeight + (e.VerticalChange / scale));
        e.Handled = true;
    }

    private void EnterCanvasFullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasFullscreen)
        {
            return;
        }

        _previousWindowStyle = WindowStyle;
        _previousWindowState = WindowState;
        _previousResizeMode = ResizeMode;
        _previousTopmost = Topmost;

        _isCanvasFullscreen = true;
        FullscreenCanvasOverlay.Visibility = Visibility.Visible;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = _previousTopmost;
        WindowState = WindowState.Maximized;

        if (DataContext is MainViewModel viewModel)
        {
            UpdateFullscreenNodeDrawer(viewModel.HasSelectedNode, false);
        }
    }

    private void ExitCanvasFullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        ExitCanvasFullscreen();
    }

    private void CloseFullscreenNodePanelButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ClearSelection();
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isCanvasFullscreen)
        {
            ExitCanvasFullscreen();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F11)
        {
            if (_isCanvasFullscreen)
            {
                ExitCanvasFullscreen();
            }
            else
            {
                EnterCanvasFullscreenButton_Click(this, new RoutedEventArgs());
            }

            e.Handled = true;
        }
    }

    private void ExitCanvasFullscreen()
    {
        if (!_isCanvasFullscreen)
        {
            return;
        }

        EndCanvasPan(null);
        _isCanvasFullscreen = false;
        UpdateFullscreenNodeDrawer(false, false);
        FullscreenCanvasOverlay.Visibility = Visibility.Collapsed;
        WindowState = WindowState.Normal;
        WindowStyle = _previousWindowStyle;
        ResizeMode = _previousResizeMode;
        Topmost = _previousTopmost;
        WindowState = _previousWindowState;
    }

    private void EndCanvasPan(FrameworkElement? surface)
    {
        if (!_isCanvasPanning)
        {
            return;
        }

        _isCanvasPanning = false;
        _canvasPanScrollViewer = null;
        if (surface is not null)
        {
            surface.Cursor = Cursors.Arrow;
        }

        Mouse.Capture(null);
    }

    private void UpdateFullscreenNodeDrawer(bool shouldShow, bool animate)
    {
        if (!_isCanvasFullscreen && shouldShow)
        {
            return;
        }

        var transform = FullscreenNodeDrawer.RenderTransform as TranslateTransform;
        if (transform is null)
        {
            transform = new TranslateTransform();
            FullscreenNodeDrawer.RenderTransform = transform;
        }

        var hiddenOffset = (FullscreenNodeDrawer.ActualHeight > 0 ? FullscreenNodeDrawer.ActualHeight : FullscreenNodeDrawer.Height) + 28d;
        if (!animate)
        {
            FullscreenNodeDrawer.BeginAnimation(OpacityProperty, null);
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            FullscreenNodeDrawer.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
            FullscreenNodeDrawer.Opacity = shouldShow ? 1d : 0d;
            transform.Y = shouldShow ? 0d : hiddenOffset;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(220);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        FullscreenNodeDrawer.BeginAnimation(OpacityProperty, null);

        if (shouldShow)
        {
            FullscreenNodeDrawer.Visibility = Visibility.Visible;
            FullscreenNodeDrawer.Opacity = 1d;
            transform.Y = hiddenOffset;
            transform.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(0d, duration) { EasingFunction = ease });
            return;
        }

        var hideAnimation = new DoubleAnimation(hiddenOffset, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = ease
        };
        hideAnimation.Completed += (_, _) =>
        {
            if (DataContext is MainViewModel viewModel && viewModel.HasSelectedNode && _isCanvasFullscreen)
            {
                return;
            }

            FullscreenNodeDrawer.Visibility = Visibility.Collapsed;
            FullscreenNodeDrawer.Opacity = 0d;
            transform.Y = hiddenOffset;
        };

        transform.BeginAnimation(TranslateTransform.YProperty, hideAnimation);
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

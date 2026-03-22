using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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
    private Point _dragOriginPoint;
    private double _nodeOriginX;
    private double _nodeOriginY;
    private HwndSource? _hwndSource;

    public MainWindow()
    {
        InitializeComponent();

        var screenCaptureService = new ScreenCaptureService();
        var visionMatcherService = new VisionMatcherService(screenCaptureService);
        var inputSimulationService = new InputSimulationService();
        var inputRecordingService = new InputRecordingService();

        DataContext = new MainViewModel(
            new JsonProjectStorageService(),
            new PreviewFlowExecutionEngine(),
            new DesktopFlowExecutionEngine(visionMatcherService, inputSimulationService),
            screenCaptureService,
            inputRecordingService);

        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
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

    private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FlowNodeViewModel nodeViewModel })
        {
            return;
        }

        _draggingNode = nodeViewModel;
        _dragOriginPoint = e.GetPosition(DesignerSurface);
        _nodeOriginX = nodeViewModel.X;
        _nodeOriginY = nodeViewModel.Y;

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SelectedNode = nodeViewModel;
        }

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

        var currentPosition = e.GetPosition(DesignerSurface);
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
        e.Handled = true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

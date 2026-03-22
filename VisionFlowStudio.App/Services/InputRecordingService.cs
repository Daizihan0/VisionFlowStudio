using System.Diagnostics;
using System.Runtime.InteropServices;
using VisionFlowStudio.Core.Models;

namespace VisionFlowStudio.App.Services;

public sealed class InputRecordingService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int WmSyskeydown = 0x0104;
    private const int WmSyskeyup = 0x0105;
    private const int WmLbuttonup = 0x0202;
    private const int WmRbuttonup = 0x0205;
    private const int WmMbuttonup = 0x0208;
    private const int WmMousewheel = 0x020A;

    private readonly object _syncRoot = new();
    private readonly HookProc _keyboardProc;
    private readonly HookProc _mouseProc;
    private readonly List<RecordedInputEvent> _events = [];
    private readonly Stopwatch _stopwatch = new();
    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;
    private int _lastEventAtMs;

    public InputRecordingService()
    {
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
    }

    public bool IsRecording { get; private set; }

    public IReadOnlyList<RecordedInputEvent> Stop()
    {
        if (!IsRecording)
        {
            return [];
        }

        Unhook();
        IsRecording = false;
        _stopwatch.Stop();

        lock (_syncRoot)
        {
            return _events.Select(CloneEvent).ToList();
        }
    }

    public void Start()
    {
        if (IsRecording)
        {
            return;
        }

        lock (_syncRoot)
        {
            _events.Clear();
            _lastEventAtMs = 0;
        }

        _keyboardHook = SetHook(_keyboardProc, WhKeyboardLl);
        _mouseHook = SetHook(_mouseProc, WhMouseLl);
        _stopwatch.Restart();
        IsRecording = true;
    }

    public void Dispose()
    {
        Unhook();
        GC.SuppressFinalize(this);
    }

    private static RecordedInputEvent CloneEvent(RecordedInputEvent source) => new()
    {
        EventType = source.EventType,
        DelayMs = source.DelayMs,
        X = source.X,
        Y = source.Y,
        MouseButton = source.MouseButton,
        WheelDelta = source.WheelDelta,
        VirtualKey = source.VirtualKey
    };

    private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && IsRecording)
        {
            var vkCode = Marshal.ReadInt32(lParam);
            if (vkCode is not (0x78 or 0x79))
            {
                if ((int)wParam == WmKeydown || (int)wParam == WmSyskeydown)
                {
                    AddEvent(new RecordedInputEvent
                    {
                        EventType = RecordedInputEventType.KeyDown,
                        VirtualKey = (ushort)vkCode
                    });
                }
                else if ((int)wParam == WmKeyup || (int)wParam == WmSyskeyup)
                {
                    AddEvent(new RecordedInputEvent
                    {
                        EventType = RecordedInputEventType.KeyUp,
                        VirtualKey = (ushort)vkCode
                    });
                }
            }
        }

        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && IsRecording)
        {
            var info = Marshal.PtrToStructure<Msllhookstruct>(lParam);
            RecordedInputEvent? recordedEvent = (int)wParam switch
            {
                WmLbuttonup => CreateMouseClick(info, RecordedMouseButton.Left),
                WmRbuttonup => CreateMouseClick(info, RecordedMouseButton.Right),
                WmMbuttonup => CreateMouseClick(info, RecordedMouseButton.Middle),
                WmMousewheel => new RecordedInputEvent
                {
                    EventType = RecordedInputEventType.MouseWheel,
                    X = info.Point.X,
                    Y = info.Point.Y,
                    WheelDelta = (short)((info.MouseData >> 16) & 0xffff)
                },
                _ => null
            };

            if (recordedEvent is not null)
            {
                AddEvent(recordedEvent);
            }
        }

        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private void AddEvent(RecordedInputEvent recordedEvent)
    {
        lock (_syncRoot)
        {
            var now = (int)_stopwatch.ElapsedMilliseconds;
            recordedEvent.DelayMs = Math.Max(0, now - _lastEventAtMs);
            _lastEventAtMs = now;
            _events.Add(recordedEvent);
        }
    }

    private static RecordedInputEvent CreateMouseClick(Msllhookstruct info, RecordedMouseButton button) => new()
    {
        EventType = RecordedInputEventType.MouseClick,
        X = info.Point.X,
        Y = info.Point.Y,
        MouseButton = button
    };

    private static IntPtr SetHook(HookProc proc, int hookType)
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        return SetWindowsHookEx(hookType, proc, GetModuleHandle(module?.ModuleName), 0);
    }

    private void Unhook()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msllhookstruct
    {
        public PointStruct Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint DwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}

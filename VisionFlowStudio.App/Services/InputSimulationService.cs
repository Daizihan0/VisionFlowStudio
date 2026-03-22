using System.Runtime.InteropServices;
using System.Windows;
using VisionFlowStudio.Core.Models;

namespace VisionFlowStudio.App.Services;

public sealed class InputSimulationService
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseeventfMove = 0x0001;
    private const uint MouseeventfLeftDown = 0x0002;
    private const uint MouseeventfLeftUp = 0x0004;
    private const uint MouseeventfRightDown = 0x0008;
    private const uint MouseeventfRightUp = 0x0010;
    private const uint MouseeventfMiddleDown = 0x0020;
    private const uint MouseeventfMiddleUp = 0x0040;
    private const uint MouseeventfWheel = 0x0800;
    private const uint MouseeventfAbsolute = 0x8000;
    private const uint MouseeventfVirtualDesk = 0x4000;
    private const uint KeyeventfKeyUp = 0x0002;
    private const uint KeyeventfUnicode = 0x0004;

    public Task LeftClickAsync(Point point, CancellationToken cancellationToken) =>
        MouseClickAsync(point, MouseeventfLeftDown, MouseeventfLeftUp, cancellationToken);

    public async Task DoubleClickAsync(Point point, CancellationToken cancellationToken)
    {
        await LeftClickAsync(point, cancellationToken);
        await Task.Delay(90, cancellationToken);
        await LeftClickAsync(point, cancellationToken);
    }

    public Task RightClickAsync(Point point, CancellationToken cancellationToken) =>
        MouseClickAsync(point, MouseeventfRightDown, MouseeventfRightUp, cancellationToken);

    public Task MiddleClickAsync(Point point, CancellationToken cancellationToken) =>
        MouseClickAsync(point, MouseeventfMiddleDown, MouseeventfMiddleUp, cancellationToken);

    public async Task MouseWheelAsync(Point point, int delta, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MoveCursor(point);
        SendMouseInput(MouseeventfWheel, mouseData: delta);
        await Task.Delay(18, cancellationToken);
    }

    public async Task ReplayRecordedEventAsync(RecordedInputEvent recordedEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (recordedEvent.DelayMs > 0)
        {
            await Task.Delay(recordedEvent.DelayMs, cancellationToken);
        }

        switch (recordedEvent.EventType)
        {
            case RecordedInputEventType.MouseClick:
                if (recordedEvent.X is null || recordedEvent.Y is null || recordedEvent.MouseButton is null)
                {
                    return;
                }

                await ReplayMouseClickAsync(recordedEvent, cancellationToken);
                break;
            case RecordedInputEventType.MouseWheel:
                if (recordedEvent.X is null || recordedEvent.Y is null)
                {
                    return;
                }

                await MouseWheelAsync(new Point(recordedEvent.X.Value, recordedEvent.Y.Value), recordedEvent.WheelDelta, cancellationToken);
                break;
            case RecordedInputEventType.KeyDown:
                await SendVirtualKeyAsync(recordedEvent.VirtualKey, isKeyUp: false, cancellationToken);
                break;
            case RecordedInputEventType.KeyUp:
                await SendVirtualKeyAsync(recordedEvent.VirtualKey, isKeyUp: true, cancellationToken);
                break;
        }
    }

    public async Task TypeTextAsync(string text, CancellationToken cancellationToken)
    {
        foreach (var character in text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendKeyboardUnicode(character, isKeyUp: false);
            SendKeyboardUnicode(character, isKeyUp: true);
            await Task.Delay(12, cancellationToken);
        }
    }

    public async Task SendHotkeyAsync(string hotkey, CancellationToken cancellationToken)
    {
        var tokens = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return;
        }

        var keyCodes = tokens.Select(ParseVirtualKey).ToArray();
        foreach (var keyCode in keyCodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendKeyboardVirtualKey(keyCode, isKeyUp: false);
            await Task.Delay(16, cancellationToken);
        }

        for (var index = keyCodes.Length - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendKeyboardVirtualKey(keyCodes[index], isKeyUp: true);
            await Task.Delay(16, cancellationToken);
        }
    }

    public async Task SendVirtualKeyAsync(ushort virtualKey, bool isKeyUp, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SendKeyboardVirtualKey(virtualKey, isKeyUp);
        await Task.Delay(12, cancellationToken);
    }

    public void MoveCursor(Point point)
    {
        var virtualScreen = VirtualScreenHelper.GetBounds();
        var normalizedX = (int)Math.Round((point.X - virtualScreen.Left) * 65535 / Math.Max(1, virtualScreen.Width - 1));
        var normalizedY = (int)Math.Round((point.Y - virtualScreen.Top) * 65535 / Math.Max(1, virtualScreen.Height - 1));

        var input = new Input
        {
            Type = InputMouse,
            Union = new InputUnion
            {
                MouseInput = new MouseInput
                {
                    Dx = normalizedX,
                    Dy = normalizedY,
                    MouseData = 0,
                    DwFlags = MouseeventfMove | MouseeventfAbsolute | MouseeventfVirtualDesk,
                    Time = 0,
                    DwExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private async Task MouseClickAsync(Point point, uint downFlag, uint upFlag, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MoveCursor(point);
        await Task.Delay(25, cancellationToken);
        SendMouseInput(downFlag);
        SendMouseInput(upFlag);
        await Task.Delay(20, cancellationToken);
    }

    private async Task ReplayMouseClickAsync(RecordedInputEvent recordedEvent, CancellationToken cancellationToken)
    {
        var point = new Point(recordedEvent.X!.Value, recordedEvent.Y!.Value);

        switch (recordedEvent.MouseButton)
        {
            case RecordedMouseButton.Left:
                await LeftClickAsync(point, cancellationToken);
                break;
            case RecordedMouseButton.Right:
                await RightClickAsync(point, cancellationToken);
                break;
            case RecordedMouseButton.Middle:
                await MiddleClickAsync(point, cancellationToken);
                break;
        }
    }

    private static void SendMouseInput(uint flags, int mouseData = 0)
    {
        var input = new Input
        {
            Type = InputMouse,
            Union = new InputUnion
            {
                MouseInput = new MouseInput
                {
                    Dx = 0,
                    Dy = 0,
                    MouseData = mouseData,
                    DwFlags = flags,
                    Time = 0,
                    DwExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private static void SendKeyboardUnicode(char character, bool isKeyUp)
    {
        var input = new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                KeyboardInput = new KeyboardInput
                {
                    WVk = 0,
                    WScan = character,
                    DwFlags = KeyeventfUnicode | (isKeyUp ? KeyeventfKeyUp : 0),
                    Time = 0,
                    DwExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private static void SendKeyboardVirtualKey(ushort virtualKey, bool isKeyUp)
    {
        var input = new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                KeyboardInput = new KeyboardInput
                {
                    WVk = virtualKey,
                    WScan = 0,
                    DwFlags = isKeyUp ? KeyeventfKeyUp : 0,
                    Time = 0,
                    DwExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    public static ushort ParseVirtualKey(string token) => token.Trim().ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => 0x11,
        "SHIFT" => 0x10,
        "ALT" => 0x12,
        "WIN" or "WINDOWS" => 0x5B,
        "ENTER" => 0x0D,
        "TAB" => 0x09,
        "ESC" or "ESCAPE" => 0x1B,
        "SPACE" => 0x20,
        "BACKSPACE" => 0x08,
        "DELETE" => 0x2E,
        "UP" => 0x26,
        "DOWN" => 0x28,
        "LEFT" => 0x25,
        "RIGHT" => 0x27,
        _ when token.Length == 1 && char.IsLetterOrDigit(token[0]) => (ushort)char.ToUpperInvariant(token[0]),
        _ when token.Length > 1 && token[0] == 'F' && int.TryParse(token[1..], out var fx) && fx is >= 1 and <= 24 => (ushort)(0x6F + fx),
        _ => throw new InvalidOperationException($"不支持的快捷键按键：{token}")
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInputStructure);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput MouseInput;

        [FieldOffset(0)]
        public KeyboardInput KeyboardInput;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public int MouseData;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort WVk;
        public ushort WScan;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }
}

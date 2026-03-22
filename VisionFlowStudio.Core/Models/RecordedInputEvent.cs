namespace VisionFlowStudio.Core.Models;

public sealed class RecordedInputEvent
{
    public RecordedInputEventType EventType { get; set; }

    public int DelayMs { get; set; }

    public double? X { get; set; }

    public double? Y { get; set; }

    public RecordedMouseButton? MouseButton { get; set; }

    public int WheelDelta { get; set; }

    public ushort VirtualKey { get; set; }
}

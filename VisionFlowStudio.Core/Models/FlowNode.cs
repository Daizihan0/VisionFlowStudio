namespace VisionFlowStudio.Core.Models;

public sealed class FlowNode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public FlowNodeKind Kind { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ProjectPoint Position { get; set; } = new();

    public int TimeoutMs { get; set; } = 10_000;

    public int RetryLimit { get; set; }

    public Dictionary<string, string> Settings { get; set; } = new();

    public string? AssetPayloadBase64 { get; set; }

    public string? AssetFileName { get; set; }

    public List<RecordedInputEvent> RecordedEvents { get; set; } = [];
}

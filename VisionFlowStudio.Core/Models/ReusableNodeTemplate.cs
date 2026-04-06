using System.Text.Json.Serialization;

namespace VisionFlowStudio.Core.Models;

public sealed class ReusableNodeTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

    public FlowNode Node { get; set; } = new();

    [JsonIgnore]
    public string KindLabel =>
        Node.Kind switch
        {
            FlowNodeKind.Start => "Start",
            FlowNodeKind.Action => "Action",
            FlowNodeKind.Vision => "Vision",
            FlowNodeKind.Wait => "Wait",
            FlowNodeKind.Condition => "Condition",
            FlowNodeKind.SubFlow => "Recorded Clip",
            FlowNodeKind.End => "End",
            _ => "Node"
        };
}

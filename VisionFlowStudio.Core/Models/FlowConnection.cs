namespace VisionFlowStudio.Core.Models;

public sealed class FlowConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceNodeId { get; set; }

    public Guid TargetNodeId { get; set; }

    public FlowConnectorKind ConnectorKind { get; set; } = FlowConnectorKind.Next;

    public string Label { get; set; } = string.Empty;
}

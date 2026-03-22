namespace VisionFlowStudio.Core.Models;

public sealed class FlowGraph
{
    public string Name { get; set; } = "主流程";

    public List<FlowNode> Nodes { get; set; } = [];

    public List<FlowConnection> Connections { get; set; } = [];
}

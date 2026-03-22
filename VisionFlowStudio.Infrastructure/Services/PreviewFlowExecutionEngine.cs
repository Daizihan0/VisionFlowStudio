using VisionFlowStudio.Core.Models;
using VisionFlowStudio.Core.Services;

namespace VisionFlowStudio.Infrastructure.Services;

public sealed class PreviewFlowExecutionEngine : IFlowExecutionEngine
{
    public async Task ExecutePreviewAsync(
        AutomationProject project,
        Action<ExecutionLogEntry> onLog,
        Action<Guid, NodeStatus> onNodeStatusChanged,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(onLog);
        ArgumentNullException.ThrowIfNull(onNodeStatusChanged);

        var graph = project.Graph;
        if (graph.Nodes.Count == 0)
        {
            onLog(CreateLog("警告", "当前流程为空，无法运行预览。"));
            return;
        }

        var nodeLookup = graph.Nodes.ToDictionary(node => node.Id);
        var current = graph.Nodes.FirstOrDefault(node => node.Kind == FlowNodeKind.Start) ?? graph.Nodes[0];
        var visitedSteps = 0;
        const int maxPreviewSteps = 128;

        onLog(CreateLog("信息", $"开始预览流程：{project.Name}"));

        while (current is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            visitedSteps++;
            if (visitedSteps > maxPreviewSteps)
            {
                onLog(CreateLog("警告", "预览已达到最大步骤数，可能存在循环未收敛。"));
                return;
            }

            onNodeStatusChanged(current.Id, NodeStatus.Running);
            onLog(CreateLog("信息", $"执行节点：{current.Title}", current.Id));

            await Task.Delay(GetPreviewDelay(current.Kind), cancellationToken);

            onNodeStatusChanged(current.Id, NodeStatus.Succeeded);

            if (current.Kind == FlowNodeKind.End)
            {
                onLog(CreateLog("成功", "流程预览执行结束。", current.Id));
                return;
            }

            var nextConnection = SelectNextConnection(graph.Connections, current);
            if (nextConnection is null)
            {
                onLog(CreateLog("信息", $"节点“{current.Title}”没有后续连线，预览停止。", current.Id));
                return;
            }

            var previousNode = current;

            if (!nodeLookup.TryGetValue(nextConnection.TargetNodeId, out current))
            {
                onLog(CreateLog("错误", $"节点“{previousNode.Title}”的目标连线已失效。", previousNode.Id));
                onNodeStatusChanged(previousNode.Id, NodeStatus.Failed);
                return;
            }
        }
    }

    private static int GetPreviewDelay(FlowNodeKind kind) =>
        kind switch
        {
            FlowNodeKind.Start => 180,
            FlowNodeKind.Wait => 600,
            FlowNodeKind.Condition => 320,
            FlowNodeKind.Vision => 480,
            FlowNodeKind.SubFlow => 520,
            FlowNodeKind.End => 160,
            _ => 260
        };

    private static FlowConnection? SelectNextConnection(IEnumerable<FlowConnection> connections, FlowNode current)
    {
        var candidates = connections
            .Where(connection => connection.SourceNodeId == current.Id)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        if (current.Kind == FlowNodeKind.Condition)
        {
            if (current.Settings.TryGetValue("ExpectedState", out var configuredState)
                && bool.TryParse(configuredState, out var state))
            {
                return candidates.FirstOrDefault(connection => connection.ConnectorKind == (state ? FlowConnectorKind.True : FlowConnectorKind.False))
                       ?? candidates[0];
            }

            return candidates.FirstOrDefault(connection => connection.ConnectorKind == FlowConnectorKind.True)
                   ?? candidates[0];
        }

        return candidates.FirstOrDefault(connection => connection.ConnectorKind is FlowConnectorKind.Next or FlowConnectorKind.Success)
               ?? candidates[0];
    }

    private static ExecutionLogEntry CreateLog(string level, string message, Guid? nodeId = null) =>
        new()
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            NodeId = nodeId
        };
}

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
        var current = ResolveEntryNode(graph, nodeLookup);
        if (current is null)
        {
            onLog(CreateLog("警告", "所有入口节点都已被停用，无法执行预览。"));
            return;
        }
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

            if (current.IsTemporarilyDisabled)
            {
                onLog(CreateLog("信息", $"节点“{current.Title}”已临时停用，已跳过。", current.Id));
                var disabledNext = SelectNextConnection(graph.Connections, current, nodeLookup);
                if (disabledNext is null || !nodeLookup.TryGetValue(disabledNext.TargetNodeId, out current))
                {
                    onLog(CreateLog("信息", "流程已到达末端。"));
                    return;
                }

                continue;
            }

            onNodeStatusChanged(current.Id, NodeStatus.Running);
            onLog(CreateLog("信息", $"执行节点：{current.Title}", current.Id));

            await Task.Delay(GetPreviewDelay(current), cancellationToken);

            onNodeStatusChanged(current.Id, NodeStatus.Succeeded);

            if (current.Kind == FlowNodeKind.End)
            {
                onLog(CreateLog("成功", "流程预览执行结束。", current.Id));
                return;
            }

            var nextConnection = SelectNextConnection(graph.Connections, current, nodeLookup);
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

    private static int GetPreviewDelay(FlowNode node)
    {
        var baseDelay = node.Kind switch
        {
            FlowNodeKind.Start => 180,
            FlowNodeKind.Wait => 600,
            FlowNodeKind.Condition => 320,
            FlowNodeKind.Vision => 480,
            FlowNodeKind.SubFlow => 520,
            FlowNodeKind.End => 160,
            _ => 260
        };

        if (node.Kind != FlowNodeKind.Action)
        {
            return baseDelay;
        }

        var beforeDelay = GetInt(node.Settings, "BeforeActionDelayMs", 0);
        var afterDelay = GetInt(node.Settings, "AfterActionDelayMs", 0);
        return baseDelay + beforeDelay + afterDelay;
    }

    private static FlowNode? ResolveEntryNode(FlowGraph graph, IReadOnlyDictionary<Guid, FlowNode> nodeLookup)
    {
        var startNode = graph.Nodes.FirstOrDefault(node => node.Kind == FlowNodeKind.Start) ?? graph.Nodes.FirstOrDefault();
        if (startNode is null)
        {
            return null;
        }

        if (!startNode.IsTemporarilyDisabled)
        {
            return startNode;
        }

        var nextConnection = SelectNextConnection(graph.Connections, startNode, nodeLookup);
        return nextConnection is not null && nodeLookup.TryGetValue(nextConnection.TargetNodeId, out var nextNode)
            ? nextNode
            : graph.Nodes.FirstOrDefault(node => !node.IsTemporarilyDisabled);
    }

    private static FlowConnection? SelectNextConnection(
        IEnumerable<FlowConnection> connections,
        FlowNode current,
        IReadOnlyDictionary<Guid, FlowNode> nodeLookup)
    {
        var candidates = connections
            .Where(connection => connection.SourceNodeId == current.Id)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var enabledCandidates = candidates
            .Where(connection => !nodeLookup.TryGetValue(connection.TargetNodeId, out var targetNode) || !targetNode.IsTemporarilyDisabled)
            .ToList();

        if (enabledCandidates.Count == 0)
        {
            return null;
        }

        if (current.Kind == FlowNodeKind.Condition)
        {
            if (current.Settings.TryGetValue("ExpectedState", out var configuredState)
                && bool.TryParse(configuredState, out var state))
            {
                return enabledCandidates.FirstOrDefault(connection => connection.ConnectorKind == (state ? FlowConnectorKind.True : FlowConnectorKind.False))
                       ?? enabledCandidates[0];
            }

            return enabledCandidates.FirstOrDefault(connection => connection.ConnectorKind == FlowConnectorKind.True)
                   ?? enabledCandidates[0];
        }

        return enabledCandidates.FirstOrDefault(connection => connection.ConnectorKind is FlowConnectorKind.Next or FlowConnectorKind.Success)
               ?? enabledCandidates[0];
    }

    private static ExecutionLogEntry CreateLog(string level, string message, Guid? nodeId = null) =>
        new()
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            NodeId = nodeId
        };

    private static int GetInt(IReadOnlyDictionary<string, string> settings, string key, int fallback) =>
        settings.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;
}

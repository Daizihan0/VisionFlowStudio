using System.Windows;
using VisionFlowStudio.Core.Models;
using VisionFlowStudio.Core.Services;

namespace VisionFlowStudio.App.Services;

public sealed class DesktopFlowExecutionEngine : IFlowExecutionEngine
{
    private readonly VisionMatcherService _visionMatcherService;
    private readonly InputSimulationService _inputSimulationService;

    public DesktopFlowExecutionEngine(
        VisionMatcherService visionMatcherService,
        InputSimulationService inputSimulationService)
    {
        _visionMatcherService = visionMatcherService;
        _inputSimulationService = inputSimulationService;
    }

    public async Task ExecutePreviewAsync(
        AutomationProject project,
        Action<ExecutionLogEntry> onLog,
        Action<Guid, NodeStatus> onNodeStatusChanged,
        CancellationToken cancellationToken)
    {
        var graph = project.Graph;
        var nodeLookup = graph.Nodes.ToDictionary(node => node.Id);
        var current = ResolveEntryNode(graph, nodeLookup);
        if (current is null)
        {
            onLog(Log("警告", "流程为空，或所有入口节点都已被停用。"));
            return;
        }

        var runtime = new RuntimeState();
        var guard = 0;

        onLog(Log("信息", $"开始真实运行：{project.Name}"));

        while (current is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            guard++;
            if (guard > 512)
            {
                onLog(Log("错误", "流程步骤超过安全上限，已中止运行。"));
                return;
            }

            if (current.IsTemporarilyDisabled)
            {
                onLog(Log("信息", $"节点“{current.Title}”已临时停用，已跳过。", current.Id));
                var disabledNext = SelectNextConnection(graph.Connections, current, runtime.LastConditionResult, nodeLookup);
                if (disabledNext is null || !nodeLookup.TryGetValue(disabledNext.TargetNodeId, out current))
                {
                    onLog(Log("信息", "流程已到达末端。"));
                    return;
                }

                continue;
            }

            onNodeStatusChanged(current.Id, NodeStatus.Running);

            try
            {
                await ExecuteNodeAsync(current, runtime, onLog, cancellationToken);
                onNodeStatusChanged(current.Id, NodeStatus.Succeeded);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                onNodeStatusChanged(current.Id, NodeStatus.Failed);
                onLog(Log("错误", $"节点“{current.Title}”执行失败：{ex.Message}", current.Id));

                var failureNext = graph.Connections.FirstOrDefault(connection =>
                    connection.SourceNodeId == current.Id
                    && connection.ConnectorKind == FlowConnectorKind.Failure
                    && (!nodeLookup.TryGetValue(connection.TargetNodeId, out var targetNode) || !targetNode.IsTemporarilyDisabled));
                if (failureNext is null || !nodeLookup.TryGetValue(failureNext.TargetNodeId, out current))
                {
                    return;
                }

                continue;
            }

            if (current.Kind == FlowNodeKind.End)
            {
                onLog(Log("成功", "真实运行已完成。", current.Id));
                return;
            }

            var next = SelectNextConnection(graph.Connections, current, runtime.LastConditionResult, nodeLookup);
            if (next is null || !nodeLookup.TryGetValue(next.TargetNodeId, out current))
            {
                onLog(Log("信息", "流程已到达末端。"));
                return;
            }
        }
    }

    private async Task ExecuteNodeAsync(FlowNode node, RuntimeState runtime, Action<ExecutionLogEntry> onLog, CancellationToken cancellationToken)
    {
        onLog(Log("信息", $"执行节点：{node.Title}", node.Id));

        switch (node.Kind)
        {
            case FlowNodeKind.Start:
                await Task.Delay(80, cancellationToken);
                break;
            case FlowNodeKind.Vision:
                await ExecuteVisionNodeAsync(node, runtime, cancellationToken);
                onLog(Log("成功", $"视觉定位成功：({Math.Round(runtime.LastAnchorPoint!.Value.X)}, {Math.Round(runtime.LastAnchorPoint.Value.Y)})", node.Id));
                break;
            case FlowNodeKind.Action:
                await ExecuteActionNodeAsync(node, runtime, cancellationToken);
                break;
            case FlowNodeKind.Wait:
                await ExecuteWaitNodeAsync(node, runtime, cancellationToken);
                break;
            case FlowNodeKind.Condition:
                runtime.LastConditionResult = await ExecuteConditionNodeAsync(node, runtime, cancellationToken);
                onLog(Log("信息", $"条件结果：{runtime.LastConditionResult}", node.Id));
                break;
            case FlowNodeKind.SubFlow:
                await ExecuteSubFlowNodeAsync(node, onLog, cancellationToken);
                break;
            case FlowNodeKind.End:
                await Task.Delay(60, cancellationToken);
                break;
        }
    }

    private async Task ExecuteVisionNodeAsync(FlowNode node, RuntimeState runtime, CancellationToken cancellationToken)
    {
        var template = ResolveTemplate(node, runtime);
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException("当前节点没有截图模板，请先使用“截图定位”或“粘贴图片”。");
        }

        var threshold = GetDouble(node.Settings, "Threshold", 0.92d);
        var match = await RetryUntilMatchAsync(template, threshold, node.TimeoutMs, node.RetryLimit, cancellationToken);
        if (match is null)
        {
            throw new InvalidOperationException("未找到匹配图像。");
        }

        runtime.LastAnchorPoint = match.Center;
        runtime.LastTemplatePayloadBase64 = template;
    }

    private async Task ExecuteActionNodeAsync(FlowNode node, RuntimeState runtime, CancellationToken cancellationToken)
    {
        var beforeDelayMs = GetInt(node.Settings, "BeforeActionDelayMs", 0);
        if (beforeDelayMs > 0)
        {
            await Task.Delay(beforeDelayMs, cancellationToken);
        }

        var actionType = GetString(node.Settings, "ActionType", "LeftClick");
        var targetMode = GetString(node.Settings, "TargetMode", "AnchorCenter");
        var targetPoint = ResolveTargetPoint(targetMode, node, runtime);

        switch (actionType.ToUpperInvariant())
        {
            case "LEFTCLICK":
                await _inputSimulationService.LeftClickAsync(targetPoint, cancellationToken);
                break;
            case "DOUBLECLICK":
                await _inputSimulationService.DoubleClickAsync(targetPoint, cancellationToken);
                break;
            case "RIGHTCLICK":
                await _inputSimulationService.RightClickAsync(targetPoint, cancellationToken);
                break;
            case "MIDDLECLICK":
                await _inputSimulationService.MiddleClickAsync(targetPoint, cancellationToken);
                break;
            case "WHEELUP":
                await _inputSimulationService.MouseWheelAsync(targetPoint, 120, cancellationToken);
                break;
            case "WHEELDOWN":
                await _inputSimulationService.MouseWheelAsync(targetPoint, -120, cancellationToken);
                break;
            case "TYPETEXT":
                await _inputSimulationService.TypeTextAsync(GetString(node.Settings, "Text", string.Empty), cancellationToken);
                break;
            case "HOTKEY":
                await _inputSimulationService.SendHotkeyAsync(GetString(node.Settings, "Hotkey", "Ctrl+V"), cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"不支持的动作类型：{actionType}");
        }

        var afterDelayMs = GetInt(node.Settings, "AfterActionDelayMs", 0);
        if (afterDelayMs > 0)
        {
            await Task.Delay(afterDelayMs, cancellationToken);
        }
    }

    private async Task ExecuteWaitNodeAsync(FlowNode node, RuntimeState runtime, CancellationToken cancellationToken)
    {
        var waitType = GetString(node.Settings, "WaitType", "Delay");
        if (waitType.Equals("Delay", StringComparison.OrdinalIgnoreCase))
        {
            var duration = GetInt(node.Settings, "DurationMs", 1000);
            await Task.Delay(duration, cancellationToken);
            return;
        }

        if (waitType.Equals("PixelEquals", StringComparison.OrdinalIgnoreCase))
        {
            var point = ParsePoint(GetString(node.Settings, "Point", string.Empty));
            var color = GetString(node.Settings, "PixelColor", "#FFFFFF");
            await WaitUntilAsync(() => Task.FromResult(_visionMatcherService.PixelMatches(point, color, GetInt(node.Settings, "Tolerance", 12))), node.TimeoutMs, cancellationToken);
            return;
        }

        var template = ResolveTemplate(node, runtime);
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException("等待节点缺少截图模板。");
        }

        var threshold = GetDouble(node.Settings, "Threshold", 0.92d);
        if (waitType.Equals("ImageDisappear", StringComparison.OrdinalIgnoreCase))
        {
            await WaitUntilAsync(async () => await _visionMatcherService.FindOnScreenAsync(template, threshold, cancellationToken) is null, node.TimeoutMs, cancellationToken);
            return;
        }

        await WaitUntilAsync(async () => await _visionMatcherService.FindOnScreenAsync(template, threshold, cancellationToken) is not null, node.TimeoutMs, cancellationToken);
    }

    private async Task<bool> ExecuteConditionNodeAsync(FlowNode node, RuntimeState runtime, CancellationToken cancellationToken)
    {
        var conditionType = GetString(node.Settings, "ConditionType", "ImageExists");

        if (conditionType.Equals("PixelEquals", StringComparison.OrdinalIgnoreCase))
        {
            var point = ParsePoint(GetString(node.Settings, "Point", string.Empty));
            var color = GetString(node.Settings, "PixelColor", "#FFFFFF");
            return _visionMatcherService.PixelMatches(point, color, GetInt(node.Settings, "Tolerance", 12));
        }

        var template = ResolveTemplate(node, runtime);
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException("条件节点缺少截图模板。");
        }

        var threshold = GetDouble(node.Settings, "Threshold", 0.92d);
        return await _visionMatcherService.FindOnScreenAsync(template, threshold, cancellationToken) is not null;
    }

    private async Task ExecuteSubFlowNodeAsync(FlowNode node, Action<ExecutionLogEntry> onLog, CancellationToken cancellationToken)
    {
        if (node.RecordedEvents.Count == 0)
        {
            throw new InvalidOperationException("当前录制片段节点还没有录制数据，请先开始录制。");
        }

        onLog(Log("信息", $"开始回放 {node.RecordedEvents.Count} 个录制事件。", node.Id));

        foreach (var recordedEvent in node.RecordedEvents)
        {
            await _inputSimulationService.ReplayRecordedEventAsync(recordedEvent, cancellationToken);
        }
    }

    private async Task<VisionMatchResult?> RetryUntilMatchAsync(string template, double threshold, int timeoutMs, int retryLimit, CancellationToken cancellationToken)
    {
        var attempt = 0;
        var startedAt = DateTime.UtcNow;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;

            var match = await _visionMatcherService.FindOnScreenAsync(template, threshold, cancellationToken);
            if (match is not null)
            {
                return match;
            }

            if (attempt > retryLimit + 1 || (DateTime.UtcNow - startedAt).TotalMilliseconds >= timeoutMs)
            {
                return null;
            }

            await Task.Delay(250, cancellationToken);
        }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate, int timeoutMs, CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await predicate())
            {
                return;
            }

            if ((DateTime.UtcNow - startedAt).TotalMilliseconds >= timeoutMs)
            {
                throw new TimeoutException("等待条件超时。");
            }

            await Task.Delay(180, cancellationToken);
        }
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

        var nextConnection = SelectNextConnection(graph.Connections, startNode, conditionResult: null, nodeLookup);
        return nextConnection is not null && nodeLookup.TryGetValue(nextConnection.TargetNodeId, out var nextNode)
            ? nextNode
            : graph.Nodes.FirstOrDefault(node => !node.IsTemporarilyDisabled);
    }

    private static FlowConnection? SelectNextConnection(
        IEnumerable<FlowConnection> connections,
        FlowNode current,
        bool? conditionResult,
        IReadOnlyDictionary<Guid, FlowNode> nodeLookup)
    {
        var candidates = connections.Where(connection => connection.SourceNodeId == current.Id).ToList();
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
            var desired = conditionResult == true ? FlowConnectorKind.True : FlowConnectorKind.False;
            return enabledCandidates.FirstOrDefault(connection => connection.ConnectorKind == desired)
                   ?? enabledCandidates.FirstOrDefault();
        }

        return enabledCandidates.FirstOrDefault(connection => connection.ConnectorKind == FlowConnectorKind.Success)
               ?? enabledCandidates.FirstOrDefault(connection => connection.ConnectorKind == FlowConnectorKind.Next)
               ?? enabledCandidates.FirstOrDefault();
    }

    private static Point ResolveTargetPoint(string targetMode, FlowNode node, RuntimeState runtime)
    {
        if (targetMode.Equals("AbsolutePoint", StringComparison.OrdinalIgnoreCase))
        {
            return ParsePoint(GetString(node.Settings, "Point", string.Empty));
        }

        if (targetMode.Equals("OffsetFromAnchor", StringComparison.OrdinalIgnoreCase))
        {
            if (runtime.LastAnchorPoint is null)
            {
                throw new InvalidOperationException("当前没有可用的视觉锚点。请先执行视觉节点。");
            }

            var offset = ParseVector(GetString(node.Settings, "ClickOffset", "0,0"));
            return new Point(runtime.LastAnchorPoint.Value.X + offset.X, runtime.LastAnchorPoint.Value.Y + offset.Y);
        }

        return runtime.LastAnchorPoint ?? ParsePoint(GetString(node.Settings, "Point", string.Empty));
    }

    private static string? ResolveTemplate(FlowNode node, RuntimeState runtime) =>
        !string.IsNullOrWhiteSpace(node.AssetPayloadBase64)
            ? node.AssetPayloadBase64
            : runtime.LastTemplatePayloadBase64;

    private static Point ParsePoint(string raw)
    {
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !double.TryParse(parts[0], out var x) || !double.TryParse(parts[1], out var y))
        {
            throw new InvalidOperationException("点位参数格式错误，应为 x,y");
        }

        return new Point(x, y);
    }

    private static Point ParseVector(string raw) => ParsePoint(raw);

    private static string GetString(IReadOnlyDictionary<string, string> settings, string key, string fallback) =>
        settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static int GetInt(IReadOnlyDictionary<string, string> settings, string key, int fallback) =>
        settings.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static double GetDouble(IReadOnlyDictionary<string, string> settings, string key, double fallback) =>
        settings.TryGetValue(key, out var value) && double.TryParse(value, out var parsed) ? parsed : fallback;

    private static ExecutionLogEntry Log(string level, string message, Guid? nodeId = null) => new()
    {
        Timestamp = DateTime.Now,
        Level = level,
        Message = message,
        NodeId = nodeId
    };

    private sealed class RuntimeState
    {
        public Point? LastAnchorPoint { get; set; }

        public string? LastTemplatePayloadBase64 { get; set; }

        public bool? LastConditionResult { get; set; }
    }
}

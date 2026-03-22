using VisionFlowStudio.Core.Models;

namespace VisionFlowStudio.Core.Services;

public interface IFlowExecutionEngine
{
    Task ExecutePreviewAsync(
        AutomationProject project,
        Action<ExecutionLogEntry> onLog,
        Action<Guid, NodeStatus> onNodeStatusChanged,
        CancellationToken cancellationToken);
}

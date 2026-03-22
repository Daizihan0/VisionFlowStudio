namespace VisionFlowStudio.Core.Models;

public sealed class ExecutionLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string Level { get; set; } = "信息";

    public string Message { get; set; } = string.Empty;

    public Guid? NodeId { get; set; }
}

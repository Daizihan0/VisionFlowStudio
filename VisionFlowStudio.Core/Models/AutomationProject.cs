namespace VisionFlowStudio.Core.Models;

public sealed class AutomationProject
{
    public string Name { get; set; } = "新建自动化项目";

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public FlowGraph Graph { get; set; } = new();
}

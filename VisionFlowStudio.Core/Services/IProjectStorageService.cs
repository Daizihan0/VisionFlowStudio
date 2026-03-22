using VisionFlowStudio.Core.Models;

namespace VisionFlowStudio.Core.Services;

public interface IProjectStorageService
{
    Task SaveAsync(AutomationProject project, string filePath, CancellationToken cancellationToken);

    Task<AutomationProject> LoadAsync(string filePath, CancellationToken cancellationToken);
}

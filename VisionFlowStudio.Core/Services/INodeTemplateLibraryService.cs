using VisionFlowStudio.Core.Models;

namespace VisionFlowStudio.Core.Services;

public interface INodeTemplateLibraryService
{
    Task<IReadOnlyList<ReusableNodeTemplate>> LoadAllAsync(CancellationToken cancellationToken);

    Task SaveAsync(ReusableNodeTemplate template, CancellationToken cancellationToken);

    Task DeleteAsync(Guid templateId, CancellationToken cancellationToken);
}

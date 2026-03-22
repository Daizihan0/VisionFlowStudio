using System.Text.Json;
using System.Text.Json.Serialization;
using VisionFlowStudio.Core.Models;
using VisionFlowStudio.Core.Services;

namespace VisionFlowStudio.Infrastructure.Services;

public sealed class JsonProjectStorageService : IProjectStorageService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task SaveAsync(AutomationProject project, string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        project.UpdatedAtUtc = DateTime.UtcNow;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, project, SerializerOptions, cancellationToken);
    }

    public async Task<AutomationProject> LoadAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using var stream = File.OpenRead(filePath);
        var project = await JsonSerializer.DeserializeAsync<AutomationProject>(stream, SerializerOptions, cancellationToken);

        return project ?? new AutomationProject();
    }
}

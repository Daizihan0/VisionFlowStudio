using System.Text.Json;
using System.Text.Json.Serialization;
using VisionFlowStudio.Core.Models;
using VisionFlowStudio.Core.Services;

namespace VisionFlowStudio.Infrastructure.Services;

public sealed class JsonNodeTemplateLibraryService : INodeTemplateLibraryService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _storageFilePath;

    public JsonNodeTemplateLibraryService(string? storageFilePath = null)
    {
        _storageFilePath = storageFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VisionFlowStudio",
            "node-templates.json");
    }

    public async Task<IReadOnlyList<ReusableNodeTemplate>> LoadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storageFilePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_storageFilePath);
        var templates = await JsonSerializer.DeserializeAsync<List<ReusableNodeTemplate>>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return templates ?? [];
    }

    public async Task SaveAsync(ReusableNodeTemplate template, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(template);

        var templates = (await LoadAllAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var existingIndex = templates.FindIndex(item => item.Id == template.Id);
        if (existingIndex >= 0)
        {
            templates[existingIndex] = template;
        }
        else
        {
            templates.Add(template);
        }

        var directory = Path.GetDirectoryName(_storageFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_storageFilePath);
        await JsonSerializer.SerializeAsync(stream, templates, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid templateId, CancellationToken cancellationToken)
    {
        var templates = (await LoadAllAsync(cancellationToken).ConfigureAwait(false))
            .Where(item => item.Id != templateId)
            .ToList();

        var directory = Path.GetDirectoryName(_storageFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_storageFilePath);
        await JsonSerializer.SerializeAsync(stream, templates, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }
}

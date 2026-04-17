using System.Text.Json;

namespace FileSync.Core;

public sealed class MetadataStore
{
    private readonly string _path;
    private readonly IAppLogger _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public MetadataStore(string path, IAppLogger logger)
    {
        _path = path;
        _logger = logger;
    }

    public IReadOnlyList<FileItem> Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return Array.Empty<FileItem>();
            }

            var raw = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<FileItem>>(raw) ?? new List<FileItem>();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to load metadata. Returning empty list.", ex);
            return Array.Empty<FileItem>();
        }
    }

    public void Save(IEnumerable<FileItem> items)
    {
        try
        {
            var json = JsonSerializer.Serialize(items, _jsonOptions);
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to save metadata.", ex);
            throw;
        }
    }
}

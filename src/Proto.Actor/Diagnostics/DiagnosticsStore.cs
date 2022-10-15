using System.Collections.Concurrent;

using System.Text.Json.Serialization;


namespace Proto.Diagnostics;

public class DiagnosticsStore
{
    private readonly ConcurrentBag<DiagnosticsEntry> _entries = new();

    public void RegisterEvent(string module, string message)
    {
        var entry = new DiagnosticsEntry(module, message, null);
        _entries.Add(entry);
    }
    
    public void RegisterObject(string module, string key, object data)
    {
        var entry = new DiagnosticsEntry(module, key, data);
        _entries.Add(entry);
    }

    public DiagnosticsEntry[] Get()
    {
        return _entries.ToArray();
    }
}

public record DiagnosticsEntry
{
    // ReSharper disable once ConvertToPrimaryConstructor
    public DiagnosticsEntry(string module, string? message, object? data)
    {
        Module = module;
        Message = message;
        Data = data;
    }

    public string Module { get;  }


    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; }


    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; }
}

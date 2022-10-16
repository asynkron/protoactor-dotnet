using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Proto.Diagnostics;

[PublicAPI]
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
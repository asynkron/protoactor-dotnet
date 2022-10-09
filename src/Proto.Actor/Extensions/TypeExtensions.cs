using Proto.Diagnostics;

namespace Proto.Extensions;

public static class TypeExtensions
{
    public static string GetMessageTypeName(this object? message)
    {
        if (message is IDiagnosticsTypeName d)
        {
            return d.GetTypeName();
        }

        return message?.GetType().Name ?? "null";
    }
}
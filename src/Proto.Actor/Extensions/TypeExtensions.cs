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

    public static string GetActorTypeName(this IActor actor)
    {
        // ReSharper disable once SuspiciousTypeConversion.Global
        if (actor is IDiagnosticsTypeName d)
        {
            return d.GetTypeName();
        }

        return actor.GetType().Name;
    }
}
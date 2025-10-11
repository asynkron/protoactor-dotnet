namespace Proto.Diagnostics;

/// <summary>
///     This interface allows specific message types to override what typename is returned for tracing and metrics.
///     e.g. MessageEnvelope can return the name of the inner message type instead of its own type name
/// </summary>
public interface IDiagnosticsTypeName
{
    string GetTypeName();
}
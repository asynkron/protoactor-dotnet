namespace Proto.Analyzers;

public static class DiagnosticIds
{
    /// <summary>
    /// The actor attempts to poison itself while waiting for a response.
    /// This is a recipe for a deadlock
    /// </summary>
    public const string DeadlockRuleId = "PA0001";

    /// <summary>
    /// The actor attempts to send a Task as a message
    /// </summary>
    public const string TaskAsStringRuleId = "PA0002";
}
using System.Diagnostics;

namespace Proto.OpenTelemetry;

/// <summary>
///     Proto.actor specific tags on the <see cref="Activity" />
/// </summary>
public static class ProtoTags
{
    /// <summary>
    ///     Activity source name for Proto.Actor created activities
    /// </summary>
    public const string ActivitySourceName = "Proto.Actor";

    /// <summary>
    ///     GetType().Name on the message
    /// </summary>
    public const string MessageType = "proto.messagetype";
    
    /// <summary>
    ///     GetType().Name on the response message
    /// </summary>
    public const string ResponseMessageType = "proto.responsemessagetype";

    /// <summary>
    ///     Message destination PID string representation
    /// </summary>
    public const string TargetPID = "proto.targetpid";

    /// <summary>
    ///     Message sender PID string representation
    /// </summary>
    public const string SenderPID = "proto.senderpid";

    /// <summary>
    ///     Current actor PID string representation, when applicable (equals TargetPID when this is a receive activity, or
    ///     SenderId when this is a
    ///     sending activity)
    /// </summary>
    public const string ActorPID = "proto.actorpid";

    /// <summary>
    ///     Type of the current actor, when applicable
    /// </summary>
    public const string ActorType = "proto.actortype";
    
    /// <summary>
    ///     Name of the current action
    /// </summary>
    public const string ActionType = "proto.action";
}
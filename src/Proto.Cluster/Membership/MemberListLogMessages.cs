using Microsoft.Extensions.Logging;

namespace Proto.Cluster;

internal static partial class MemberListLogMessages
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Blocking member {MemberId} due to {Reason}")]
    internal static partial void BlockingMemberDueToReason(this ILogger logger, string memberId, string reason);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "MemberList did not find any activator for kind '{Kind}'")]
    internal static partial void DidNotFindActivatorForKind(this ILogger logger, string kind);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "[MemberList] Cluster members joined {MembersJoined}")]
    internal static partial void ClusterMembersJoined(this ILogger logger, object? membersJoined);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "[MemberList] Cluster members left {MembersLeft}")]
    internal static partial void ClusterMembersLeft(this ILogger logger, object? membersLeft);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Duplicate address {Address} found, removing {Rest}")]
    internal static partial void DuplicateAddressFound(this ILogger logger, string address, object? rest);

    [LoggerMessage(EventId = 5, Level = LogLevel.Critical, Message = "I have been blocked, exiting {Id}")]
    internal static partial void BlockedExiting(this ILogger logger, string id);
}


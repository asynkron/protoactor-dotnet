using System;

namespace Proto.Cluster;

internal record DiagnosticsMemberHeartbeat(string MemberId, MemberHeartbeat Value, DateTimeOffset Timestamp);
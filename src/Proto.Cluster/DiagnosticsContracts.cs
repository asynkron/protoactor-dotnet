using System;
using JetBrains.Annotations;

namespace Proto.Cluster;

[PublicAPI]
internal record DiagnosticsMemberHeartbeat(string MemberId, MemberHeartbeat Value, DateTimeOffset Timestamp);
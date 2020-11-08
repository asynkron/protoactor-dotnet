using System;

namespace Proto.Cluster.Testing
{
    internal class DeadMember : IEquatable<DeadMember>
    {
        public DeadMember(string memberId, DateTimeOffset foundDeadAt, TimeSpan deregisterInterval)
        {
            MemberId = memberId;
            FoundDeadAt = foundDeadAt;
            DeregisterInterval = deregisterInterval;
        }

        public string MemberId { get; }
        public DateTimeOffset FoundDeadAt { get; }
        public TimeSpan DeregisterInterval { get; }

        public bool Equals(DeadMember other) => !ReferenceEquals(null, other) &&
                                                (ReferenceEquals(this, other) || MemberId == other.MemberId);

        public override bool Equals(object obj)
            => !ReferenceEquals(null, obj) &&
               (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((DeadMember) obj));

        public override int GetHashCode() => MemberId is not null ? MemberId.GetHashCode() : 0;
    }
}
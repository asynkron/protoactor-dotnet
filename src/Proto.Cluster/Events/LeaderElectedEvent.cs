using JetBrains.Annotations;
using Proto.Cluster.Data;

namespace Proto.Cluster.Events
{
    [PublicAPI]
    public class LeaderElectedEvent
    {
        public LeaderElectedEvent(LeaderInfo newLeader, LeaderInfo oldLeader)
        {
            NewLeader = newLeader;
            OldLeader = oldLeader;
        }

        public LeaderInfo NewLeader { get; }
        public LeaderInfo OldLeader { get; }
    }
}
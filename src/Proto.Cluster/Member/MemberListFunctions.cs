using System.Collections.Immutable;
using System.Linq;

namespace Proto.Cluster
{
    public static class MemberListFunctions
    {
        public static ClusterTopology GetNewTopology(
            uint topologyHash,
            Member[] members,
            ImmutableDictionary<string, Member> oldMembers,
            string[] banned
        )
        {
            //these are the member IDs hashset of currently active members
            var memberIds =
                members
                    .Select(s => s.Id)
                    .ToImmutableHashSet();
            
            var left = oldMembers
                .Where(m => !memberIds.Contains(m.Key))
                .Select(m => m.Value)
                .ToArray();
            
            var joined = members
                .Where(m => !oldMembers.ContainsKey(m.Id))
                .ToArray();

            var topology = new ClusterTopology
            {
                TopologyHash = topologyHash,
                Members = {
                    members
                },
                Banned = {
                    banned
                },
                Left = {
                    left
                },
                Joined = {
                    joined
                }
            };

            return topology;
        }
    }
}
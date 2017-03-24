using System;
using System.Threading.Tasks;

namespace Proto.Cluster
{
    public static class MemberList
    {
        public static PID PID;

        public static void SubscribeToEventStream()
        {
            Actor.EventStream.Subscribe<ClusterTopologyEvent>(PID.Tell);
        }

        public static void Spawn()
        {
            PID = Actor.SpawnNamed(Actor.FromProducer(() => new MemberListActor()), "memberlist");
        }

        public static async Task<string[]> GetMembersAsync(string kind)
        {
            var res = await PID.RequestAsync<MemberByKindResponse>(new MemberByKindRequest(kind, true));
            return res.Kinds;
        }

        private static readonly Random Random = new Random();

        public static async Task<string> GetRandomActivatorAsync(string kind)
        {
            var r = Random.Next();
            var members = await GetMembersAsync(kind);
            return members[r % members.Length];
        }

    }

    public class MemberByKindResponse
    {
        public MemberByKindResponse(string[] kinds)
        {
            Kinds = kinds ??
            throw new ArgumentNullException(nameof(kinds));
        }

        public string[] Kinds { get; set; }
    }

    public class MemberByKindRequest
    {
        public MemberByKindRequest(string kind, bool onlyAlive)
        {
            Kind = kind ??
            throw new ArgumentNullException(nameof(kind));
            OnlyAlive = onlyAlive;
        }

        public string Kind { get; }
        public bool OnlyAlive { get; }
    }
}
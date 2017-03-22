using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto.Cluster
{
    internal static class Partition
    {
        public static Dictionary<string, PID> KindMap = new Dictionary<string, PID>();

        public static PID SpawnPartitionActor(string kind)
        {
            var pid = Actor.SpawnNamed(Actor.FromProducer(() => new PartitionActor(kind)), "partition-" + kind);
            return pid;
        }

        public static void SubscribePartitionKindsToEventStream()
        {
            EventStream.Instance.Subscribe<MemberStatusEvent>(msg =>
            {
                foreach (var kind in msg.Kinds)
                    if (KindMap.TryGetValue(kind, out var kindPid))
                        kindPid.Tell(msg);
            });
        }

        public static PID PartitionForKind(string address, string kind)
        {
            return new PID(address, "partition-" + kind);
        }
    }

    internal class PartitionActor : IActor
    {
        private readonly string _kind;

        private Dictionary<string, PID> _partition = new Dictionary<string, PID>(); //actor/grain name to PID

        public PartitionActor(string kind)
        {
            _kind = kind;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    //TODO: Log started
                    break;
            }
        }
    }
}
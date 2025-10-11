using Google.Protobuf.WellKnownTypes;
using Proto.Cluster.Gossip;
using Proto.Cluster.PubSub;
using Proto.Cluster.Seed;
using Proto.Remote;

namespace Proto.Cluster;

/// <summary>
///     Provides helper methods used during cluster initialization.
/// </summary>
internal static class ClusterInitialization
{
    public static void RegisterExtensions(ActorSystem system, Cluster cluster)
    {
        system.Extensions.Register(cluster);
        _ = new PubSubExtension(cluster);
    }

    public static void RegisterSerialization(ActorSystem system)
    {
        var serialization = system.Serialization();
        serialization.RegisterFileDescriptor(ClusterContractsReflection.Descriptor);
        serialization.RegisterFileDescriptor(GossipContractsReflection.Descriptor);
        serialization.RegisterFileDescriptor(PubSubContractsReflection.Descriptor);
        serialization.RegisterFileDescriptor(GrainContractsReflection.Descriptor);
        serialization.RegisterFileDescriptor(SeedContractsReflection.Descriptor);
        serialization.RegisterFileDescriptor(EmptyReflection.Descriptor);
    }
}

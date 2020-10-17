using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Proto.Cluster.Testing;

namespace Proto.Cluster.Tests
{
    public abstract class ClusterFixture
    {
        protected async Task<IList<Cluster>> SpawnClusters(int count, Action<ClusterConfig> configure = null)
        {
            var agent = new InMemAgent();
            var clusterTasks = Enumerable.Range(0, count).Select(_ => SpawnCluster(agent,configure))
                .ToList();
            await Task.WhenAll(clusterTasks);
            return clusterTasks.Select(task => task.Result).ToList();
        }

        protected async Task<Cluster> SpawnCluster(InMemAgent agent, Action<ClusterConfig> configure)
        {
            var config = new ClusterConfig("testCluster", "127.0.0.1", 0,
                    new TestProvider(new TestProviderOptions(), agent)
                )
                .WithRemoteConfig(remoteConfig => remoteConfig
                    .WithProtoMessages(Remote.Tests.Messages.ProtosReflection.Descriptor)
                )
                .WithClusterKind(EchoActor.Kind, EchoActor.Props);

            configure?.Invoke(config);
            
            var cluster = new Cluster(new ActorSystem(),config);

            await cluster.StartMemberAsync();
            return cluster;
        }
    }
}
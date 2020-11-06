namespace Proto.Cluster.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using ClusterTest.Messages;

    public static class Extensions
    {
        public static Task<Pong> Ping(this Cluster cluster, string id, string message, CancellationToken token, string kind = EchoActor.Kind)
            => cluster.RequestAsync<Pong>(id, kind, new Ping {Message = message}, token);
    }
}
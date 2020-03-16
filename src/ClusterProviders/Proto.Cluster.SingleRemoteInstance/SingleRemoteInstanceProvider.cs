using Proto.Remote;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.SingleRemoteInstance
{
    public class SingleRemoteInstanceProvider : IClusterProvider
    {
        private const string KindsResponder = "remote_kinds_responder";
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

        private readonly string _serverHost;
        private readonly int _serverPort;
        private readonly string _serverAddress;

        private string[] _kinds;
        private IMemberStatusValue _okStatus;
        private IMemberStatusValue _koStatus;
        private bool _isServer;
        private bool _shutdown;

        public SingleRemoteInstanceProvider(string serverHost, int serverPort)
        {
            _serverHost = serverHost;
            _serverPort = serverPort;
            _serverAddress = $"{serverHost}:{serverPort}";
        }

        public Task DeregisterMemberAsync(Cluster cluster) => Actor.Done;

        public Task RegisterMemberAsync(Cluster cluster, string clusterName, string host, int port, string[] kinds, IMemberStatusValue statusValue, IMemberStatusValueSerializer serializer)
        {
            var props = Props.FromFunc(ctx =>
                        {
                            if (ctx.Message is GetKinds && ctx.Sender != null)
                                ctx.Respond(new GetKindsResponse { Kinds = { _kinds } });

                            return Actor.Done;
                        });
            cluster.Remote.Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);

            cluster.Remote.RegisterKnownKind(KindsResponder, props);

            _kinds = kinds;
            _okStatus = serializer.Deserialize("Ok!");
            _koStatus = serializer.Deserialize("Ko!");

            _isServer = host == _serverHost && port == _serverPort;

            return Actor.Done;
        }

        public void MonitorMemberStatusChanges(Cluster cluster)
        {
            var t = new Thread(_ =>
            {
                while (!_shutdown)
                {
                    NotifyStatuses(cluster);
                }
            })
            { IsBackground = true };
            t.Start();
        }

        public Task Shutdown(Cluster cluster)
        {
            _shutdown = true;
            return Actor.Done;
        }

        public Task UpdateMemberStatusValueAsync(Cluster cluster, IMemberStatusValue statusValue) => Actor.Done;

        private void NotifyStatuses(Cluster cluster)
        {
            MemberStatus status;
            if (_isServer)
            {
                status = new MemberStatus(_serverAddress, _serverHost, _serverPort, _kinds, true, _okStatus);
            }
            else
            {
                var responder = cluster.Remote.SpawnNamedAsync(_serverAddress, KindsResponder, KindsResponder, _timeout).Result;
                if (responder.Pid != null)
                {
                    try
                    {
                        var response = cluster.System.Root.RequestAsync<GetKindsResponse>(responder.Pid, new GetKinds(), _timeout).Result;
                        status = new MemberStatus(_serverAddress, _serverHost, _serverPort, response.Kinds, true, _okStatus);
                    }
                    catch (Exception ex) when (ex is TimeoutException || ex.InnerException is TimeoutException)
                    {
                        status = new MemberStatus(_serverAddress, _serverHost, _serverPort, new string[0], true, _koStatus);
                    }
                }
                else
                {
                    status = new MemberStatus(_serverAddress, _serverHost, _serverPort, new string[0], false, _koStatus);
                }
            }

            var @event = new ClusterTopologyEvent(new[] { status });
            cluster.System.EventStream.Publish(@event);

            Thread.Sleep(TimeSpan.FromMinutes(1));
        }

    }
}

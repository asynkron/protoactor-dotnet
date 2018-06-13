using Proto.Remote;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.SingleRemoteInstance
{
    public class SingleRemoteInstanceProvider : IClusterProvider
    {
        const string KINDS_RESPONDER = "remote_kinds_responder";
        readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

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

            var props = Props.FromFunc(ctx =>
            {
                if ((ctx.Message is GetKinds) && ctx.Sender != null)
                    ctx.Respond(new GetKindsResponse { Kinds = { _kinds } });

                return Actor.Done;
            });

            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            //RootContext.Empty.SpawnNamed(props, KINDS_RESPONDER);

            Remote.Remote.RegisterKnownKind(KINDS_RESPONDER, props);
        }

        public Task DeregisterMemberAsync() => Actor.Done;

        public Task RegisterMemberAsync(string clusterName, string host, int port, string[] kinds, IMemberStatusValue statusValue, IMemberStatusValueSerializer serializer)
        {
            _kinds = kinds;
            _okStatus = serializer.FromValueBytes(Encoding.UTF8.GetBytes("Ok!"));
            _koStatus = serializer.FromValueBytes(Encoding.UTF8.GetBytes("Ko!"));

            _isServer = host == _serverHost && port == _serverPort;

            return Actor.Done;
        }

        public void MonitorMemberStatusChanges()
        {
            var t = new Thread(_ =>
            {
                while (!_shutdown)
                {
                    NotifyStatuses();
                }
            })
            { IsBackground = true };
            t.Start();
        }

        public Task Shutdown()
        {
            _shutdown = true;
            return Actor.Done;
        }

        public Task UpdateMemberStatusValueAsync(IMemberStatusValue statusValue) => Actor.Done;

        private void NotifyStatuses()
        {
            MemberStatus status = null;
            if (_isServer)
            {
                status = new MemberStatus(_serverAddress, _serverHost, _serverPort, _kinds, true, _okStatus);
            }
            else
            {
                var responder = Remote.Remote.SpawnNamedAsync(_serverAddress, KINDS_RESPONDER, KINDS_RESPONDER, _timeout).Result;
                if (responder.Pid != null)
                {
                    try
                    {
                        var response = RootContext.Empty.RequestAsync<GetKindsResponse>(responder.Pid, new GetKinds(), _timeout).Result;
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
            Actor.EventStream.Publish(@event);

            Thread.Sleep(TimeSpan.FromMinutes(1));
        }

    }
}

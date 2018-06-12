using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.SingleRemoteInstance
{
    public class SingleRemoteInstanceProvider : IClusterProvider
    {
        private string _host;
        private int _port;
        private string[] _kinds;
        private IMemberStatusValueSerializer _statusValueSerializer;
        private IMemberStatusValue _okStatus;
        private bool _shutdown;

        public Task DeregisterMemberAsync() => Actor.Done;

        public Task RegisterMemberAsync(string clusterName, string host, int port, string[] kinds, IMemberStatusValue statusValue, IMemberStatusValueSerializer serializer)
        {
            _host = host;
            _port = port;
            _kinds = kinds;
            _statusValueSerializer = serializer;
            _okStatus = _statusValueSerializer.FromValueBytes(Encoding.UTF8.GetBytes("Ok!"));
            return Actor.Done;
        }

        public Task Shutdown()
        {
            _shutdown = true;
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

        public Task UpdateMemberStatusValueAsync(IMemberStatusValue statusValue) => Actor.Done;

        private void NotifyStatuses()
        {
            Thread.Sleep(TimeSpan.FromMinutes(1));
            var res = new ClusterTopologyEvent(new[] { new MemberStatus($"{_host}:{_port}", _host, _port, _kinds, true, _okStatus) });
            Actor.EventStream.Publish(res);
        }

    }
}

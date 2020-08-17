using System;
using JetBrains.Annotations;

namespace Proto.Cluster
{
    [PublicAPI]
    public class LeaderStatus
    {
        public LeaderStatus(Guid memberId, string host, int port)
        {
            MemberId = memberId;
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Port = port;
        }

        public string Address => Host + ":" + Port;
        public Guid MemberId { get; }
        public string Host { get; }
        public int Port { get; }

        public override string ToString() => $"LeaderStatus Address:{Address} ID:{MemberId}";
    }
}
// -----------------------------------------------------------------------
//   <copyright file="MemberStatusEvents.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Cluster
{
    public class ClusterTopologyEvent
    {
        public ClusterTopologyEvent(IEnumerable<MemberStatus> statuses)
            => Statuses = statuses?.ToArray() ?? throw new ArgumentNullException(nameof(statuses));

        public IReadOnlyCollection<MemberStatus> Statuses { get; }
    }

    public abstract class MemberStatusEvent
    {
        protected MemberStatusEvent(Guid id, string host, int port, IReadOnlyCollection<string> kinds)
        {
            Id = id;
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Kinds = kinds ?? throw new ArgumentNullException(nameof(kinds));
            Port = port;
        }

        public Guid Id { get; }
        public string Address => Host + ":" + Port;
        public string Host { get; }
        public int Port { get; }
        public IReadOnlyCollection<string> Kinds { get; }

        public override string ToString()
        {
            return $"{GetType().Name} Address:{Address} ID:{Id}";
        }
    }

    public class MemberJoinedEvent : MemberStatusEvent
    {
        public MemberJoinedEvent(Guid id, string host, int port, IReadOnlyCollection<string> kinds) : base(id, host, port, kinds) { }
    }

    public class MemberLeftEvent : MemberStatusEvent
    {
        public MemberLeftEvent(Guid id, string host, int port, IReadOnlyCollection<string> kinds) : base(id, host, port, kinds) { }
    }
}
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
        {
            Statuses = statuses?.ToArray() ?? throw new ArgumentNullException(nameof(statuses));
        }

        public IReadOnlyCollection<MemberStatus> Statuses { get; }
    }

    public abstract class MemberStatusEvent
    {
        protected MemberStatusEvent(MemberStatus member)
        {
            Member = member;
        }

        public MemberStatus Member { get; }


        public override string ToString() => $"{GetType().Name} Address:{Member.Address} ID:{Member.MemberId}";
    }

    public class MemberJoinedEvent : MemberStatusEvent
    {
        public MemberJoinedEvent(MemberStatus member) : base(member)
        {
        }
    }

    public class MemberLeftEvent : MemberStatusEvent
    {
        public MemberLeftEvent(MemberStatus member) : base(member)
        {
        }
    }
}
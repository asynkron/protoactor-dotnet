// -----------------------------------------------------------------------
//   <copyright file="MemberStatusEvents.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using Proto.Cluster.Data;

namespace Proto.Cluster.Events
{
    public abstract class MemberStatusEvent
    {
        protected MemberStatusEvent(MemberInfo member)
        {
            Member = member;
        }

        public MemberInfo Member { get; }


        public override string ToString() => $"{GetType().Name} Address:{Member.Address} ID:{Member.MemberId}";
    }

    public class MemberJoinedEvent : MemberStatusEvent
    {
        public MemberJoinedEvent(MemberInfo member) : base(member)
        {
        }
    }

    public class MemberLeftEvent : MemberStatusEvent
    {
        public MemberLeftEvent(MemberInfo member) : base(member)
        {
        }
    }
}
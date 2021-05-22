// -----------------------------------------------------------------------
// <copyright file="ClusterCacheInvalidation.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Proto.Extensions;

namespace Proto.Cluster.Cache
{
    public class ClusterCacheInvalidation : IActorSystemExtension<ClusterCacheInvalidation>
    {
        private const string ActorName = "$invalidator";

        public ClusterCacheInvalidation(Cluster cluster)
        {
            Cluster = cluster;
            Cluster.System.Extensions.Register(this);
            Cluster.System.Root.SpawnNamed(
                Props.FromFunc(context => {
                        if (context.Message is ActivationTerminated terminated)
                            cluster.PidCache.RemoveByVal(terminated.ClusterIdentity, terminated.Pid);

                        return Task.CompletedTask;
                    }
                ),
                ActorName
            );
        }

        public Cluster Cluster { get; }

        private bool IsRemote(PID? sender) => sender?.Address != null && !sender.Address.Equals(Cluster.System.Address);

        private void Invalidate(ClusterIdentity identity, PID activation, BitArray activeRemotes)
        {
            var message = new ActivationTerminated
            {
                ClusterIdentity = identity,
                Pid = activation
            };
            var remotesToInvalidate = Cluster.MemberList.GetAllMembers()
                .Select(m => Cluster.MemberList.GetMetaMember(m.Id))
                .Where(m => activeRemotes.Length > m!.Index && activeRemotes[m.Index])
                .Select(m => m!.Member.Address);

            foreach (var address in remotesToInvalidate)
            {
                Cluster.System.Root.Send(PID.FromAddress(address, ActorName), message);
            }
        }

        private void AddRemote(PID sender, BitArray activeRemotes)
        {
            if (Cluster.MemberList.TryGetMemberIndexByAddress(sender.Address, out var index))
            {
                if (index >= activeRemotes.Length) activeRemotes.Length = index + 1;

                activeRemotes[index] = true;
            }
        }

        internal Action<MessageEnvelope> ForActor(ClusterIdentity identity, PID activation)
        {
            var activeRemotes = new BitArray(16);
            return envelope => {
                if (envelope.Message is Stopped) Invalidate(identity, activation, activeRemotes);
                else if (IsRemote(envelope.Sender)) AddRemote(envelope.Sender!, activeRemotes);
            };
        }
    }
}
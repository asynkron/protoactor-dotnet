// -----------------------------------------------------------------------
// <copyright file="ClusterCacheInvalidation.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            ActorSystem = cluster.System;
            ActorSystem.Extensions.Register(this);

            ActorSystem.EventStream.Subscribe<ClusterTopology>(topology => {
                    foreach (var member in topology.Joined)
                    {
                        if (member.Id.Equals(ActorSystem.Id)) continue;

                        AddressRefs.TryAdd(member.Address, NextId++);
                    }

                    foreach (var member in topology.Left)
                    {
                        AddressRefs.Remove(member.Address, out _);
                    }
                }
            );

            ActorSystem.Root.SpawnNamed(
                Props.FromFunc(context => {
                        if (context.Message is ActivationTerminated terminated)
                            cluster.PidCache.RemoveByVal(terminated.ClusterIdentity, terminated.Pid);

                        return Task.CompletedTask;
                    }
                ),
                ActorName
            );
        }

        private ConcurrentDictionary<string, int> AddressRefs { get; } = new();
        private ActorSystem ActorSystem { get; }
        private int NextId { get; set; }

        private bool IsRemote(PID? sender) => sender?.Address != null && !sender.Address.Equals(ActorSystem.Address);

        private void Invalidate(ClusterIdentity identity, PID activation, BitArray activeRemotes)
        {
            var message = new ActivationTerminated
            {
                ClusterIdentity = identity,
                Pid = activation
            };
            var remotesToInvalidate = AddressRefs
                .Where(pair => activeRemotes.Length > pair.Value && activeRemotes[pair.Value])
                .Select(pair => pair.Key);

            foreach (var address in remotesToInvalidate)
            {
                ActorSystem.Root.Send(PID.FromAddress(address, ActorName), message);
            }
        }

        private void AddRemote(PID sender, BitArray activeRemotes)
        {
            if (AddressRefs.TryGetValue(sender.Address, out var index))
            {
                if (index >= activeRemotes.Length) activeRemotes.Length = index + 1;

                activeRemotes[index] = true;
            }
        }

        internal Action<MessageEnvelope> ForActor(ClusterIdentity identity, PID activation)
        {
            var activeRemotes = new BitArray(NextId);
            return envelope => {
                if (envelope.Message is Stopped) Invalidate(identity, activation, activeRemotes);
                else if (IsRemote(envelope.Sender)) AddRemote(envelope.Sender!, activeRemotes);
            };
        }
    }
}
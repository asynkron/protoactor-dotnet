// -----------------------------------------------------------------------
// <copyright file="LocalAffinityPropsExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Identity;
using Proto.Utils;

namespace Proto.Cluster
{
    public static class LocalAffinityExtensions
    {
        private static readonly ILogger Logger = Log.CreateLogger(nameof(LocalAffinityExtensions));

        public static ClusterKind WithLocalAffinityRelocationStrategy(this ClusterKind clusterKind, LocalAffinityOptions? options = null)
            => clusterKind with
            {
                Props = clusterKind.Props.WithRelocateOnRemoteSender(options?.RelocationThroughput?.Create(), options?.TriggersLocalAffinity),
                StrategyBuilder = cluster => new LocalAffinityStrategy(cluster)
            };


        /// <summary>
        ///     Adds middleware which relocates the virtual actor on remote traffic
        ///     Useful with local affinity strategy to move partitioned workloads to the right node after a re-balance
        /// </summary>
        /// <param name="props"></param>
        /// <param name="throttle">Throttling max relocations per timespan</param>
        /// <param name="hasLocalAffinity">Predicate on message envelope, to have local affinity only on partitioned messages</param>
        /// <returns></returns>
        private static Props WithRelocateOnRemoteSender(
            this Props props,
            ShouldThrottle? throttle = null,
            Predicate<MessageEnvelope>? hasLocalAffinity = null
        )
        {
            hasLocalAffinity ??= _ => true;

            return props.WithReceiverMiddleware(receiver => (context, envelope) => {
                    var task = receiver(context, envelope);
                    //Sender is removed from context after call
                    var sender = context.Sender;
                    if (sender is null || envelope.Message is PoisonPill || context.IsMarkedForRelocation()) return task;

                    if (sender.IsRemote(context)
                     && hasLocalAffinity(envelope)
                     && throttle?.Invoke() != Throttle.Valve.Closed
                    )
                    {
                        var self = context.Self;
                        Logger.LogDebug("Relocating {ActorPid}, because of {MessageType} from {Sender}", context.Self,
                            envelope.Message.GetType(), sender
                        );
                        context.MarkForRelocation();
                        context.System.Root.PoisonAsync(self).ContinueWith(_ => ActivateByProxy(context, sender!.Address, context.Get<ClusterIdentity>()!, self));
                    }

                    return task;
                }
            );
        }

        private static bool IsMarkedForRelocation(this IContextStore context) => context.Get<Tombstone>() is not null;

        private static void MarkForRelocation(this IContextStore context) => context.Set(Tombstone.Instance);

        /// <summary>
        /// Marks the actor, to avoid poisoning twice
        /// </summary>
        private class Tombstone
        {
            public static Tombstone Instance => new();
        }

        private static Task ActivateByProxy(IReceiverContext context, string address, ClusterIdentity id, PID activation)
        {
            context.System.Root.Send(PID.FromAddress(address, IdentityActivatorProxy.ActorName),
                new ProxyActivationRequest
                {
                    ClusterIdentity = id,
                    ReplacedActivation = activation
                }
            );
            return Task.CompletedTask;
        }

        private static Func<bool> CreateShouldRelocate(float relocationFactor)
        {
            if (relocationFactor >= 1) return () => true;

            var random = new Random();
            return () => random.NextDouble() < relocationFactor;
        }

        private static bool IsRemote(this PID? sender, IInfoContext context)
            => sender is not null && !sender.Address.Equals(context.System.Address, StringComparison.OrdinalIgnoreCase);
    }
}
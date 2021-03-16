// -----------------------------------------------------------------------
// <copyright file="ClusterExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Metrics;

namespace Proto.Cluster
{
    public static class Extensions
    {
        public static ActorSystem WithCluster(this ActorSystem system, ClusterConfig config)
        {
            _ = new Cluster(system, config);
            return system;
        }

        public static Cluster Cluster(this ActorSystem system)
            => system.Extensions.Get<Cluster>() ?? throw new NotSupportedException("Cluster has not been configured");

        public static Cluster Cluster(this IContext context)
            => context.System.Extensions.Get<Cluster>() ?? throw new NotSupportedException("Cluster has not been configured");

        public static Task<T> ClusterRequestAsync<T>(this IContext context, string identity, string kind, object message, CancellationToken ct)
        {
            var cluster = context.System.Extensions.Get<Cluster>();
            //call cluster RequestAsync using actor context
            return cluster.RequestAsync<T>(identity, kind, message, context, ct);
        }

        public static Props WithClusterInit(this Props props, Cluster cluster, ClusterIdentity clusterIdentity)
        {
            return props.WithReceiverMiddleware(
                baseReceive =>
                    (ctx, env) => {
                        return env.Message switch
                        {
                            Started => HandleStart(cluster, clusterIdentity, baseReceive, ctx, env),
                            Stopped => HandleStopped(cluster, clusterIdentity, baseReceive, ctx, env),
                            _       => baseReceive(ctx, env)
                        };
                    }
            );

            static async Task HandleStart(
                Cluster cluster,
                ClusterIdentity clusterIdentity,
                Receiver baseReceive,
                IReceiverContext ctx,
                MessageEnvelope startEnvelope
            )
            {
                await baseReceive(ctx, startEnvelope);
                var grainInit = new ClusterInit(clusterIdentity, cluster);
                var grainInitEnvelope = new MessageEnvelope(grainInit, null);
                cluster.System.Metrics.Get<ClusterMetrics>()?.ClusterActorCount.Inc( new []{cluster.System.Id,cluster.System.Address,  clusterIdentity.Kind});
                await baseReceive(ctx, grainInitEnvelope);
            }

            static async Task HandleStopped(
                Cluster cluster,
                ClusterIdentity clusterIdentity,
                Receiver baseReceive,
                IReceiverContext ctx,
                MessageEnvelope startEnvelope
            )
            {
                cluster.System.Metrics.Get<ClusterMetrics>()?.ClusterActorCount.Inc( new[]{cluster.System.Id,cluster.System.Address, clusterIdentity.Kind},-1);
                await baseReceive(ctx, startEnvelope);
            }
        }

        public static Props WithRequestDedupe(this Props props, TimeSpan dedupeWindow)
        {
            var dedupe = new PidDedupe(dedupeWindow);
            return props.WithReceiverMiddleware(
                baseReceive => (ctx, env) => {
                    if (env.Sender is null)
                    {
                        baseReceive(ctx, env);
                    }

                    return dedupe.Dedupe(env.Sender!,() => baseReceive(ctx, env));
                }
            );
        }

        internal delegate bool GetInternalMemberId(PID pid, out int memberId);

        /// <summary>
        /// Will deduplicate on a sender id if the sender is an unnamed actor (ie a FutureProcess)
        /// </summary>
        internal class PidDedupe
        {
            private static readonly ILogger Logger = Log.CreateLogger<PidDedupe>();
            private readonly long _ttl;

            private readonly GetInternalMemberId _getMember;
            private readonly List<DedupeItem> _processed = new(50);

            public PidDedupe(TimeSpan dedupeInterval)
            {
                _ttl = Stopwatch.Frequency * (long) dedupeInterval.TotalSeconds;
                _getMember = (PID pid, out int id) => {
                    id = pid.Address.GetHashCode(); //Replace with lookup
                    return true;
                };
            }

            public async Task Dedupe(PID sender, Func<Task> continuation)
            {
                if (TryGetRef(sender, out var pidRef))
                {
                    if (IsDuplicate(ref pidRef))
                    {
                        Logger.LogWarning("Cluster request deduplicated");
                        return;
                    }

                    await continuation();
                    Add(pidRef);
                    return;
                }

                await continuation();
            }

            public bool IsDuplicate(ref PidRef sender)
            {
                Clean();

                foreach (var tuple in _processed)
                {
                    if (tuple.Sender.Equals(sender)) return true;
                }

                return false;
            }

            public void Add(PidRef sender) => _processed.Add(new DedupeItem(Stopwatch.GetTimestamp(), sender));

            private void Clean()
            {
                if (_processed.Count == 0) return;

                var cutoff = Stopwatch.GetTimestamp() - _ttl;

                if (!HasTimedOut(_processed[0]))
                {
                    // None have timed out
                    return;
                }

                if (HasTimedOut(_processed[^1]))
                {
                    // All have timed out
                    _processed.Clear();
                    return;
                }

                if (_processed.Count < 50 || !HasTimedOut(_processed[_processed.Count/2])) return;

                RemoveOlderThan(cutoff);

                bool HasTimedOut(DedupeItem item) => item.Ticks < cutoff;
            }

            private void RemoveOlderThan(long cutoff)
            {
                var index = _processed.BinarySearch(new DedupeItem(cutoff, default), DedupeItem.TimestampComparer);
                if (index < 0) index = ~index;
                _processed.RemoveRange(0, index + 1);
            }

            /// <summary>
            /// Will only get unnamed process id's, assuming a format of "$[0-9]*"
            /// </summary>
            /// <param name="pid">Sender PID</param>
            /// <param name="pidRef"></param>
            /// <returns></returns>
            public bool TryGetRef(PID? pid, out PidRef pidRef)
            {
                if (pid is not null && int.TryParse(pid.Id.Substring(1), out var id) && _getMember(pid, out var memberId))
                {
                    pidRef = new PidRef(memberId, id);
                    return true;
                }

                pidRef = default;
                return false;
            }

            public readonly struct PidRef
            {
                public int MemberId { get; }
                public int Id { get; }

                public PidRef(int memberId, int id)
                {
                    MemberId = memberId;
                    Id = id;
                }

                public bool Equals(PidRef other) => MemberId == other.MemberId && Id == other.Id;

                public override bool Equals(object? obj) => obj is PidRef other && Equals(other);

                public override int GetHashCode() => HashCode.Combine(MemberId, Id);
            }

            private readonly struct DedupeItem
            {
                public long Ticks { get; }
                public PidRef Sender { get; }

                public DedupeItem(long timestamp, PidRef pidRef)
                {
                    Ticks = timestamp;
                    Sender = pidRef;
                }

                private sealed class TimestampRelationalComparer : IComparer<DedupeItem>
                {
                    public int Compare(DedupeItem x, DedupeItem y) => x.Ticks.CompareTo(y.Ticks);
                }

                public static IComparer<DedupeItem> TimestampComparer { get; } = new TimestampRelationalComparer();
            }
        }
    }
}
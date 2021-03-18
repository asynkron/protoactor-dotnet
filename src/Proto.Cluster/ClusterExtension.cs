// -----------------------------------------------------------------------
// <copyright file="ClusterExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
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
                cluster.System.Metrics.Get<ClusterMetrics>()?.ClusterActorCount
                    .Inc(new[] {cluster.System.Id, cluster.System.Address, clusterIdentity.Kind});
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
                cluster.System.Metrics.Get<ClusterMetrics>()?.ClusterActorCount
                    .Inc(new[] {cluster.System.Id, cluster.System.Address, clusterIdentity.Kind}, -1);
                await baseReceive(ctx, startEnvelope);
            }
        }

        /// <summary>
        /// De-duplicates processing when receiving multiple requests from the same FutureProcess PID.
        /// Allows clients to retry requests on the same future, but not have it processed multiple times.
        /// To guarantee that the message is processed at most once, the deduplication window has to be longer than the cluster request retry window. 
        /// </summary>
        /// <param name="props"></param>
        /// <param name="dedupeWindow"></param>
        /// <returns></returns>
        public static Props WithSenderDeduplication(this Props props, TimeSpan? dedupeWindow = null)
            => props.WithContextDecorator(context => new DeduplicationContext(context, dedupeWindow));
    }

    internal class DeduplicationContext : ActorContextDecorator
    {
        private readonly PidDeDuplicator _deDuplicator;

        public DeduplicationContext([NotNull] IContext context, TimeSpan? deDuplicationWindow) : base(context)
        {
            var cluster = context.System.Cluster();
            _deDuplicator = new PidDeDuplicator(deDuplicationWindow ?? cluster.Config.ClusterRequestDeDuplicationWindow,
                cluster.MemberList.TryGetMemberIndexByAddress
            );
        }

        public override Task Receive(MessageEnvelope envelope) => envelope.Sender is null
            ? base.Receive(envelope)
            : _deDuplicator.DeDuplicate(envelope.Sender!, () => base.Receive(envelope));
    }

    internal delegate bool TryGetMemberIndex(string address, out int memberIndex);

    /// <summary>
    /// Will deduplicate on a sender id if the sender is an unnamed actor (ie a FutureProcess)
    /// </summary>
    internal class PidDeDuplicator
    {
        private static readonly ILogger Logger = Log.CreateLogger<PidDeDuplicator>();
        private readonly long _ttl;
        private long _lastCheck;
        private long _oldest;
        private long _cleanedAt;

        private readonly TryGetMemberIndex _tryGetMemberIndex;
        private readonly Dictionary<PidRef, long> _processed = new(50);

        public PidDeDuplicator(TimeSpan dedupeInterval, TryGetMemberIndex tryGetMemberIndex)
        {
            _ttl = Stopwatch.Frequency * (long) dedupeInterval.TotalSeconds;
            _tryGetMemberIndex = tryGetMemberIndex;
        }

        public async Task DeDuplicate(PID sender, Func<Task> continuation)
        {
            if (TryGetRef(sender, out var pidRef))
            {
                var now = Stopwatch.GetTimestamp();
                var cutoff = now - _ttl;

                if (IsDuplicate(pidRef, cutoff))
                {
                    Logger.LogInformation("Cluster request de-duplicated");
                    return;
                }

                await continuation();
                CleanIfNeeded(cutoff, now);
                _lastCheck = now;
                Add(pidRef, now);
                return;
            }

            await continuation();
        }

        private bool IsDuplicate(PidRef sender, long cutoff)
            => _lastCheck > cutoff && (_processed.TryGetValue(sender, out var ticks) && ticks >= cutoff);

        private void Add(PidRef sender, long now)
        {
            if (_processed.Count == 0)
            {
                _oldest = now;
            }

            _processed.Add(sender, now);
        }

        private void CleanIfNeeded(long cutoff, long now)
        {
            if (_lastCheck < cutoff)
            {
                _processed.Clear();
                _cleanedAt = now;
                _oldest = 0;
            }
            else if (_processed.Count >= 50 && _cleanedAt < _oldest)
            {
                var oldest = long.MaxValue;

                foreach (var (key, timestamp) in _processed.ToList())
                {
                    if (timestamp < cutoff)
                    {
                        _processed.Remove(key);
                    }
                    else
                    {
                        oldest = Math.Min(timestamp, oldest);
                    }
                }

                _cleanedAt = now;
                _oldest = oldest;
            }
        }

        /// <summary>
        /// Will only get unnamed process id's, assuming a format of "$[0-9]*"
        /// </summary>
        /// <param name="pid">Sender PID</param>
        /// <param name="pidRef"></param>
        /// <returns></returns>
        private bool TryGetRef(PID? pid, out PidRef pidRef)
        {
            if (pid is not null && int.TryParse(pid.Id.Substring(1), out var id) && _tryGetMemberIndex(pid.Address, out var memberId))
            {
                pidRef = new PidRef(memberId, id);
                return true;
            }

            pidRef = default;
            return false;
        }

        private readonly struct PidRef
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
    }
}
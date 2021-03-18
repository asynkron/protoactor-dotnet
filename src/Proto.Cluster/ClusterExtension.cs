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
using Proto.Deduplication;

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
        /// <param name="deduplicationWindow"></param>
        /// <returns></returns>
        public static Props WithClusterRequestDeduplication(this Props props, TimeSpan? deduplicationWindow = null)
            => props.WithContextDecorator(context => {
                    var cluster = context.System.Cluster();
                    var memberList = cluster.MemberList;

                    return new DeduplicationContext<PidRef>(context, deduplicationWindow ?? cluster.Config.ClusterRequestDeDuplicationWindow, TryGetRef);

                    bool TryGetRef(MessageEnvelope envelope, out PidRef pidRef)
                    {
                        var pid = envelope.Sender;
                        if (pid is not null && int.TryParse(pid.Id[1..], out var id) &&
                            memberList.TryGetMemberIndexByAddress(pid.Address, out var memberId))
                        {
                            pidRef = new PidRef(memberId, id);
                            return true;
                        }

                        pidRef = default;
                        return false;
                    }
                }
            );

        private readonly struct PidRef : IEquatable<PidRef>
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
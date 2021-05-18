// -----------------------------------------------------------------------
// <copyright file="ClusterExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Cluster.Metrics;
using Proto.Deduplication;

namespace Proto.Cluster
{
    [PublicAPI]
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

        public static Task<T> ClusterRequestAsync<T>(this IContext context, string identity, string kind, object message, CancellationToken ct) =>
            //call cluster RequestAsync using actor context
            context.System.Cluster()!.RequestAsync<T>(identity, kind, message, context, ct);

        public static Props WithClusterIdentity(this Props props, ClusterIdentity clusterIdentity)
            => props.WithOnInit(context => context.Set(clusterIdentity));

        internal static Props WithClusterKind(
            this Props props,
            ActivatedClusterKind clusterKind
        )
        {
            return props
                .WithReceiverMiddleware(
                    baseReceive =>
                        (ctx, env) => {
                            return env.Message switch
                            {
                                Started => HandleStart(baseReceive, ctx, env),
                                Stopped => HandleStopped(baseReceive, ctx, env),
                                _       => baseReceive(ctx, env)
                            };
                        }
                );

            async Task HandleStart(
                Receiver baseReceive,
                IReceiverContext ctx,
                MessageEnvelope startEnvelope
            )
            {
                await baseReceive(ctx, startEnvelope);
                var identity = ctx.Get<ClusterIdentity>();
                var cluster = ctx.System.Cluster();
                var grainInit = new ClusterInit(identity!, cluster);
                var grainInitEnvelope = new MessageEnvelope(grainInit, null);
                var count = clusterKind.Inc();
                cluster.System.Metrics.Get<ClusterMetrics>().ClusterActorGauge
                    .Set(count, new[] {cluster.System.Id, cluster.System.Address, clusterKind.Name});
                await baseReceive(ctx, grainInitEnvelope);
            }

            async Task HandleStopped(
                Receiver baseReceive,
                IReceiverContext ctx,
                MessageEnvelope stopEnvelope
            )
            {
                var count = clusterKind.Dec();
                var cluster = ctx.System.Cluster();
                cluster.System.Metrics.Get<ClusterMetrics>().ClusterActorGauge
                    .Set(count, new[] {cluster.System.Id, cluster.System.Address, clusterKind.Name});
                await baseReceive(ctx, stopEnvelope);
            }
        }

        /// <summary>
        ///     De-duplicates processing when receiving multiple requests from the same FutureProcess PID.
        ///     Allows clients to retry requests on the same future, but not have it processed multiple times.
        ///     To guarantee that the message is processed at most once, the deduplication window has to be longer than the cluster
        ///     request retry window.
        /// </summary>
        /// <param name="props"></param>
        /// <param name="deduplicationWindow"></param>
        /// <returns></returns>
        public static Props WithClusterRequestDeduplication(this Props props, TimeSpan? deduplicationWindow = null)
            => props.WithContextDecorator(context => {
                    var cluster = context.System.Cluster();
                    var memberList = cluster.MemberList;

                    return new DeduplicationContext<PidRef>(context, deduplicationWindow ?? cluster.Config.ClusterRequestDeDuplicationWindow,
                        TryGetRef
                    );

                    bool TryGetRef(MessageEnvelope envelope, out PidRef pidRef)
                    {
                        var pid = envelope.Sender;

                        if (pid is not null && pid.RequestId > 0 && int.TryParse(pid.Id[1..], out var id) &&
                            memberList.TryGetMemberIndexByAddress(pid.Address, out var memberId))
                        {
                            pidRef = new PidRef(memberId, id, pid.RequestId);
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
            public uint RequestId { get; }

            public PidRef(int memberId, int id, uint requestId)
            {
                MemberId = memberId;
                Id = id;
                RequestId = requestId;
            }

            public bool Equals(PidRef other) => MemberId == other.MemberId && Id == other.Id && RequestId == other.RequestId;

            public override bool Equals(object? obj) => obj is PidRef other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(MemberId, Id, RequestId);
        }
    }
}
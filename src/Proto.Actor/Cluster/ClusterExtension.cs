// -----------------------------------------------------------------------
// <copyright file="ClusterExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Deduplication;

namespace Proto.Cluster;

[PublicAPI]
public static class Extensions
{
    /// <summary>
    ///     Adds the <see cref="Proto.Cluster.Cluster" /> extension to the given <see cref="ActorSystem" />
    /// </summary>
    /// <param name="system"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static ActorSystem WithCluster(this ActorSystem system, ClusterConfig config)
    {
        _ = new Cluster(system, config);

        return system;
    }

    /// <summary>
    ///     Gets the <see cref="Proto.Cluster.Cluster" /> from the <see cref="ActorSystem" />
    /// </summary>
    /// <param name="system"></param>
    /// <returns></returns>
    public static Cluster Cluster(this ActorSystem system) =>
        system.Extensions.GetRequired<Cluster>("Cluster has not been configured");

    /// <summary>
    ///     Gets the <see cref="Proto.Cluster.Cluster" /> from the <see cref="IContext" />
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static Cluster Cluster(this IContext context) =>
        context.System.Extensions.GetRequired<Cluster>("Cluster has not been configured");

    /// <summary>
    ///     Sends a request to a cluster identity
    /// </summary>
    /// <param name="context"></param>
    /// <param name="identity">Identity to send to</param>
    /// <param name="kind">Cluster kind to sent to</param>
    /// <param name="message">Message to send</param>
    /// <param name="ct">Token to cancel the request</param>
    /// <typeparam name="T">Type of the expected response</typeparam>
    /// <returns>Response or null if timed out</returns>
    public static Task<T> ClusterRequestAsync<T>(
        this IContext context,
        string identity,
        string kind,
        object message,
        CancellationToken ct
    ) =>
        //call cluster RequestAsync using actor context
        context.System.Cluster().RequestAsync<T>(identity, kind, message, context, ct);

    /// <summary>
    ///     Sends a request to a cluster identity and calls the provided callback when the response is received. The callback
    ///     is executed within the
    ///     actor's concurrency constraint.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="identity">Identity to send to</param>
    /// <param name="kind">Cluster kind to sent to</param>
    /// <param name="message">Message to send</param>
    /// <param name="callback">
    ///     Callback that will be called after request is finished. It receives the request task as a
    ///     parameter.
    /// </param>
    /// <param name="ct">Token to cancel the request</param>
    /// <typeparam name="T">Type of the expected response</typeparam>
    public static void ClusterRequestReenter<T>(
        this IContext context,
        string identity,
        string kind,
        object message,
        Func<Task<T>, Task> callback,
        CancellationToken ct
    )
    {
        //call cluster RequestReenter using actor context
        var task = context.System.Cluster().RequestAsync<T>(identity, kind, message, context, ct);
        context.ReenterAfter(task, callback);
    }

    /// <summary>
    ///     Sends a request to a cluster identity and calls the provided callback when the response is received. The callback
    ///     is executed within the
    ///     actor's concurrency constraint.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="clusterIdentity"><see cref="ClusterIdentity" /> to send to</param>
    /// <param name="message">Message to send</param>
    /// <param name="callback">
    ///     Callback that will be called after request is finished. It receives the request task as a
    ///     parameter.
    /// </param>
    /// <param name="ct">Token to cancel the request</param>
    /// <typeparam name="T">Type of the expected response</typeparam>
    public static void ClusterRequestReenter<T>(
        this IContext context,
        ClusterIdentity clusterIdentity,
        object message,
        Func<Task<T>, Task> callback,
        CancellationToken ct
    )
    {
        //call cluster RequestReenter using actor context
        var task = context.System.Cluster().RequestAsync<T>(clusterIdentity, message, context, ct);
        context.ReenterAfter(task, callback);
    }

    internal static Props WithClusterKind(
        this Props props,
        ActivatedClusterKind clusterKind
    )
    {
        return props
            .WithReceiverMiddleware(
                baseReceive =>
                    (ctx, env) =>
                    {
                        return env.Message switch
                        {
                            Started    => HandleStarted(baseReceive, ctx, env),
                            Restarting => HandleRestarting(baseReceive, ctx, env),
                            Stopped    => HandleStopped(baseReceive, ctx, env),
                            _          => baseReceive(ctx, env)
                        };
                    }
            );

        async Task HandleStarted(
            Receiver baseReceive,
            IReceiverContext ctx,
            MessageEnvelope startEnvelope
        )
        {
            clusterKind.Inc();
            await baseReceive(ctx, startEnvelope).ConfigureAwait(false);
        }

        async Task HandleRestarting(
            Receiver baseReceive,
            IReceiverContext ctx,
            MessageEnvelope restartingEnvelope
        )
        {
            await baseReceive(ctx, restartingEnvelope).ConfigureAwait(false);

            // at this point the counter has been incremented by the Starting handler
            // Restarting means that the actor is currently stopping (but it won't get Stopping message)
            // the counter needs to be decremented to prepare for the Started message that follows next
            // (unless the base handler for Restarting throws, but then we don't reach the decrement line)
            clusterKind.Dec();
        }

        async Task HandleStopped(
            Receiver baseReceive,
            IReceiverContext ctx,
            MessageEnvelope stopEnvelope
        )
        {
            clusterKind.Dec();
            var cluster = ctx.System.Cluster();
            var identity = ctx.Get<ClusterIdentity>();

            if (identity is not null)
            {
                ctx.System.EventStream.Publish(new ActivationTerminating
                    {
                        Pid = ctx.Self,
                        ClusterIdentity = identity
                    }
                );

                cluster.PidCache.RemoveByVal(identity, ctx.Self);
            }

            await baseReceive(ctx, stopEnvelope).ConfigureAwait(false);
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
    public static Props WithClusterRequestDeduplication(this Props props, TimeSpan? deduplicationWindow = null) =>
        props.WithContextDecorator(context =>
            {
                var cluster = context.System.Cluster();
                var memberList = cluster.MemberList;

                return new DeduplicationContext<PidRef>(context,
                    deduplicationWindow ?? cluster.Config.ClusterRequestDeDuplicationWindow,
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

        public bool Equals(PidRef other) =>
            MemberId == other.MemberId && Id == other.Id && RequestId == other.RequestId;

        public override bool Equals(object? obj) => obj is PidRef other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(MemberId, Id, RequestId);
    }
}
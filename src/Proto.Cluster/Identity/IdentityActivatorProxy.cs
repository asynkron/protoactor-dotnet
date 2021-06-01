// -----------------------------------------------------------------------
// <copyright file="IdentityProxyWorker.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Identity
{
    /// <summary>
    /// Allows cluster nodes to ask other nodes to instantiate virtual actors on their behalf
    /// This is relevant when the calling node does not have the correct kind, or in combination with
    /// local affinity strategies, to relocate virtual actor activations to the correct partition
    /// </summary>
    class IdentityActivatorProxy : IActor
    {
        public const string ActorName = "$proxy-activator";

        private static readonly ILogger Logger = Log.CreateLogger<IdentityActivatorProxy>();
        private const int MaxReplaceAttempts = 5;

        public IdentityActivatorProxy(Cluster cluster)
        {
            PidCache = cluster.PidCache;
            IdentityLookup = cluster.IdentityLookup;
        }

        private IIdentityLookup IdentityLookup { get; }
        private PidCache PidCache { get; }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            ProxyActivationRequest activationRequest => Activate(activationRequest, context),
            _                                        => Task.CompletedTask
        };

        private Task Activate(ProxyActivationRequest activationRequest, IContext context)
            => activationRequest.ReplacedActivation switch
            {
                { } existing => ReplaceActivation(activationRequest.ClusterIdentity, existing, context),
                _            => Activate(activationRequest.ClusterIdentity, context)
            };

        private Task Activate(ClusterIdentity identity, IContext context)
        {
            var target = GetPid(identity, context.CancellationToken);

            if (context.Sender is not null)
            {
                context.ReenterAfter(target,
                    task => {
                        var pid = task.IsCompletedSuccessfully ? task.Result : null;
                        Respond(context, pid);
                        return Task.CompletedTask;
                    }
                );
            }

            return Task.CompletedTask;
        }

        private static void Respond(IContext context, PID? result)
        {
            if (context.Sender is null) return;

            context.Respond(new ActivationResponse
                {
                    Pid = result
                }
            );
        }

        private Task ReplaceActivation(ClusterIdentity identity, PID replacedPid, IContext context, int attempt = 1)
        {
            if (PidCache.TryGet(identity, out var current))
            {
                if (!current.Equals(replacedPid))
                {
                    // The PID has most likely already been replaced
                    // Could also be stale, but we can assume it is not to give a fast happy path.
                    // If it is stale, the caller will fix it when attempting to call it. 
                    Respond(context, current);
                    return Task.CompletedTask;
                }

                //Stale cache
                PidCache.RemoveByVal(identity, replacedPid);
            }

            context.ReenterAfter(GetPid(identity, context.CancellationToken),
                task => {
                    var activation = task.IsCompletedSuccessfully ? task.Result : null;

                    // Check if retrieved PID is stale. Replace should be called after the original activation has been stopped,
                    // but the identity might not have been purged from IdentityLookup yet.
                    if (replacedPid.Equals(activation))
                    {
                        if (attempt <= MaxReplaceAttempts)
                        {
                            Logger.LogDebug("Stale PID {Pid} from IdentityLookup when replacing {ClusterIdentity}. Will retry, attempt {Attempt} ", replacedPid, identity, attempt);
                            context.ReenterAfter(Task.Delay(50 * attempt), () => ReplaceActivation(identity, replacedPid, context, attempt + 1));
                        }
                        else
                        {
                            Logger.LogWarning("Stale PID {Pid} from IdentityLookup when replacing {ClusterIdentity}. Retries exhausted", replacedPid, identity);
                        }
                    }

                    Respond(context, activation);
                    return Task.CompletedTask;
                }
            );
            return Task.CompletedTask;
        }

        private Task<PID?> GetPid(ClusterIdentity identity, CancellationToken ct) => IdentityLookup.GetAsync(identity, ct);
    }
}
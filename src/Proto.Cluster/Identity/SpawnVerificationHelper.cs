// -----------------------------------------------------------------------
// <copyright file="SpawnVerificationHelper.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Identity;

/// <summary>
///     Helper class for shared spawn verification logic used by activator actors.
/// </summary>
internal static class SpawnVerificationHelper
{
    /// <summary>
    ///     Verifies if an identity can be spawned and calls the appropriate action.
    /// </summary>
    public static async Task VerifyAndSpawn(
        ActivationRequest msg,
        IContext context,
        ActivatedClusterKind clusterKind,
        Cluster cluster,
        HashSet<ClusterIdentity> inFlightIdentityChecks,
        Action<ActivationRequest, IContext, ActivatedClusterKind> spawn,
        ILogger logger,
        string logPrefix)
    {
        var clusterIdentity = msg.ClusterIdentity;

        if (inFlightIdentityChecks.Contains(clusterIdentity))
        {
            logger.LogError("{LogPrefix} Duplicate activation requests for {ClusterIdentity}",
                logPrefix, clusterIdentity);

            RespondWithFailure(context);

            return;
        }

        var canSpawn = clusterKind.CanSpawnIdentity!(msg.Identity,
            CancellationTokens.FromSeconds(cluster.Config.ActorSpawnVerificationTimeout));

        if (canSpawn.IsCompleted)
        {
            var canSpawnIdentity = await canSpawn.AsTask().ConfigureAwait(false);
            OnSpawnDecided(msg, context, clusterKind, canSpawnIdentity, spawn);

            return;
        }

        inFlightIdentityChecks.Add(clusterIdentity);

        context.ReenterAfter(canSpawn.AsTask(), async task =>
            {
                inFlightIdentityChecks.Remove(clusterIdentity);

                if (task.IsCompletedSuccessfully)
                {
                    var canSpawnIdentity = await task.ConfigureAwait(false);
                    OnSpawnDecided(msg, context, clusterKind, canSpawnIdentity, spawn);
                }
                else
                {
                    logger.LogError("{LogPrefix} Error when checking {ClusterIdentity}",
                        logPrefix, clusterIdentity);

                    RespondWithFailure(context);
                }
            }
        );
    }

    private static void RespondWithFailure(IContext context) =>
        context.Respond(new ActivationResponse { Failed = true });

    /// <summary>
    ///     Handles the spawn decision after verification.
    /// </summary>
    public static void OnSpawnDecided(
        ActivationRequest msg,
        IContext context,
        ActivatedClusterKind clusterKind,
        bool canSpawnIdentity,
        Action<ActivationRequest, IContext, ActivatedClusterKind> spawn)
    {
        if (canSpawnIdentity)
        {
            spawn(msg, context, clusterKind);
        }
        else
        {
            context.Respond(new ActivationResponse
                {
                    Failed = true,
                    InvalidIdentity = true
                }
            );
        }
    }
}

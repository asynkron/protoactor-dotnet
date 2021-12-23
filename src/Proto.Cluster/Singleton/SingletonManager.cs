// -----------------------------------------------------------------------
// <copyright file="SingletonManager.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Grpc.Core;
using Proto.Cluster.Gossip;
using Proto.Utils;

namespace Proto.Cluster.Singleton
{
    public class SingletonManager
    {
        private readonly Cluster _cluster;
        private ImmutableHashSet<string> _tracked = ImmutableHashSet<string>.Empty;
        private readonly object _lock = new();

        public SingletonManager(Cluster cluster)
        {
            _cluster = cluster;

            cluster.System.EventStream.Subscribe<Gossip.Gossip>(g => {
                    var tracked = _tracked;
                    var existingKeys = (
                            from member in g.State.Members
                            from entry in member.Value.Values
                            where entry.Key.StartsWith("Singleton-")
                            select entry.Key)
                        //.Distinct()
                        .ToImmutableHashSet();

                    var missing = tracked.Except(existingKeys);
                    
                    //iterate over all missing actors
                    //send a touch message to them to activate
                    foreach (var m in missing)
                    {
                        _ = cluster.RequestAsync<Touched>("", "", new Touch(), CancellationTokens.FromSeconds(5));
                    }
                }
            );
        }

        public void Track(ClusterIdentity identity)
        {
            lock (_lock)
            {
                _tracked = _tracked.Add(Key(identity));
            }
        }

        public void Untrack(ClusterIdentity identity)
        {
            lock (_lock)
            {
                _tracked = _tracked.Remove(Key(identity));
            }
        }
        
        private static string Key(ClusterIdentity identity) => "Singleton-" + identity.ToDiagnosticString();
    }
}
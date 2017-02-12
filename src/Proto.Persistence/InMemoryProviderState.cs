// -----------------------------------------------------------------------
//  <copyright file="InMemoryProviderState.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Google.Protobuf;

namespace Proto.Persistence
{
    internal class InMemoryProviderState : IProviderState
    {
        private readonly IDictionary<string, List<object>> _events = new ConcurrentDictionary<string, List<object>>();

        private readonly IDictionary<string, Tuple<object, int>> _snapshots =
            new Dictionary<string, Tuple<object, int>>();

        public void Restart()
        {
        }

        public int GetSnapshotInterval()
        {
            return 0;
        }

        public Tuple<object, int> GetSnapshot(string actorName)
        {
            Tuple<object, int> snapshot;
            return _snapshots.TryGetValue(actorName, out snapshot)
                ? snapshot
                : null;
        }

        public void GetEvents(string actorName, int eventIndexStart, Action<object> callback)
        {
            List<object> events;
            if (_events.TryGetValue(actorName, out events))
            {
                foreach (var e in events)
                {
                    callback(e);
                }
            }
        }

        public void PersistEvent(string actorName, int eventIndex, IMessage @event)
        {
            List<object> events;
            if (_events.TryGetValue(actorName, out events))
            {
                events.Add(@event);
            }
        }

        public void PersistSnapshot(string actorName, int eventIndex, IMessage snapshot)
        {
            _snapshots[actorName] = Tuple.Create((object) snapshot, eventIndex);
        }
    }
}
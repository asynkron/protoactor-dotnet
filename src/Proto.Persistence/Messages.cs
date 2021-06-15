// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Persistence
{
    public class Event
    {
        public Event(object data, long index)
        {
            Data = data;
            Index = index;
        }

        public object Data { get; }
        public long Index { get; }
    }

    public class PersistedSnapshot : Snapshot
    {
        public PersistedSnapshot(object state, long index) : base(state, index)
        {
        }
    }

    public class RecoverSnapshot : Snapshot
    {
        public RecoverSnapshot(object state, long index) : base(state, index)
        {
        }
    }
    
    public class Snapshot
    {
        public Snapshot(object state, long index)
        {
            State = state;
            Index = index;
        }

        public object State { get; }
        public long Index { get; }
    }
    
    
    public class RecoverEvent : Event
    {
        public RecoverEvent(object data, long index) : base(data, index)
        {
        }
    }

    public class ReplayEvent : Event
    {
        public ReplayEvent(object data, long index) : base(data, index)
        {
        }
    }

    public class PersistedEvent : Event
    {
        public PersistedEvent(object data, long index) : base(data, index)
        {
        }
    }
}
// -----------------------------------------------------------------------
//  <copyright file="InMemoryProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

namespace Proto.Persistence
{
    public class InMemoryProvider : IProvider
    {
        private readonly InMemoryProviderState _state;

        public static InMemoryProvider Default()
        {
            var state=  new InMemoryProviderState();
            return new InMemoryProvider(state);
        }

        // allow passing in of InMemoryProviderState to allow state to "persist" across actor restarts
        public InMemoryProvider(InMemoryProviderState state)
        {
            _state = state;
        }

        public IEventState GetEventState()
        {
            return _state;
        }

        public ISnapshotState GetSnapshotState()
        {
            return _state;
        }
    }
}
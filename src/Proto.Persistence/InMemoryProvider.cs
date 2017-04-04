// -----------------------------------------------------------------------
//  <copyright file="InMemoryProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

namespace Proto.Persistence
{
    public class InMemoryProvider : IProvider
    {
        private readonly IProviderState _state;
        // allow passing in of IProviderState to allow state to "persist" across actor restarts
        public InMemoryProvider(InMemoryProviderState state = null)
        {
            _state = state;
        }
        public IProviderState GetState()
        {
            return _state ?? new InMemoryProviderState();
        }
    }
}
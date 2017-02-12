// -----------------------------------------------------------------------
//  <copyright file="InMemoryProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

namespace Proto.Persistence
{
    public class InMemoryProvider : IProvider
    {
        public IProviderState GetState()
        {
            return new InMemoryProviderState();
        }
    }
}
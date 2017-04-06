// -----------------------------------------------------------------------
//  <copyright file="IProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

namespace Proto.Persistence
{
    public interface IProvider
    {
        IEventState GetEventState();
        ISnapshotState GetSnapshotState();
    }

    public class SeparateEventAndSnapshotStateProvider : IProvider
    {
        private readonly IProvider _eventProvider;
        private readonly IProvider _snapshotProvider;

        public SeparateEventAndSnapshotStateProvider(IProvider eventProvider, IProvider snapshotProvider)
        {
            _eventProvider = eventProvider;
            _snapshotProvider = snapshotProvider;
        }
        
        public IEventState GetEventState()
        {
            return _eventProvider.GetEventState();
        }

        public ISnapshotState GetSnapshotState()
        {
            return _snapshotProvider.GetSnapshotState();
        }
    }
}
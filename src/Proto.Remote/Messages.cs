// -----------------------------------------------------------------------
//  <copyright file="Messages.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

namespace Proto.Remote
{
    public sealed class EndpointTerminatedEvent
    {
        public string Address { get; set; }
    }

    public class RemoteTerminate
    {
        public RemoteTerminate(PID watcher, PID watchee)
        {
            Watcher = watcher;
            Watchee = watchee;
        }

        public PID Watcher { get; }
        public PID Watchee { get; }
    }

    public class RemoteWatch
    {
        public RemoteWatch(PID watcher, PID watchee)
        {
            Watcher = watcher;
            Watchee = watchee;
        }

        public PID Watcher { get; }
        public PID Watchee { get; }
    }

    public class RemoteUnwatch
    {
        public RemoteUnwatch(PID watcher, PID watchee)
        {
            Watcher = watcher;
            Watchee = watchee;
        }

        public PID Watcher { get; }
        public PID Watchee { get; }
    }
}
// -----------------------------------------------------------------------
// <copyright file="Supervision.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

// ReSharper disable once CheckNamespace
namespace Proto
{
    public class Supervision
    {
        private ActorSystem _system;

        public Supervision(ActorSystem system)
        {
            _system = system;
            DefaultStrategy = new OneForOneStrategy(_system,(who, reason) => SupervisorDirective.Restart, 10, TimeSpan.FromSeconds(10));
            AlwaysRestartStrategy = new AlwaysRestartStrategy();
        }
        public ISupervisorStrategy DefaultStrategy { get; } 
            

        public ISupervisorStrategy AlwaysRestartStrategy { get; } 
    }

    public delegate SupervisorDirective Decider(PID pid, Exception reason);
}
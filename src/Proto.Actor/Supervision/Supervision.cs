// -----------------------------------------------------------------------
// <copyright file="Supervision.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

// ReSharper disable once CheckNamespace
namespace Proto
{
    public static class Supervision
    {
        public static ISupervisorStrategy DefaultStrategy { get; } =
            new OneForOneStrategy((who, reason) => SupervisorDirective.Restart, 10, TimeSpan.FromSeconds(10));

        public static ISupervisorStrategy AlwaysRestartStrategy { get; } = new AlwaysRestartStrategy();
    }

    public delegate SupervisorDirective Decider(PID pid, Exception reason);
}
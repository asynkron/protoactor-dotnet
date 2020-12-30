// -----------------------------------------------------------------------
// <copyright file="ISupervisor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;

// ReSharper disable once CheckNamespace
namespace Proto
{
    public interface ISupervisor
    {
        IImmutableSet<PID> Children { get; }

        void EscalateFailure(Exception reason, object? message);

        void RestartChildren(Exception reason, params PID[] pids);

        void StopChildren(params PID[] pids);

        void ResumeChildren(params PID[] pids);
    }
}
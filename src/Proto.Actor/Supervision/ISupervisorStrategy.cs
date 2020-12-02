// -----------------------------------------------------------------------
// <copyright file="ISupervisorStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

// ReSharper disable once CheckNamespace
namespace Proto
{
    public interface ISupervisorStrategy
    {
        void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception cause, object? message);
    }
}
// -----------------------------------------------------------------------
// <copyright file="IFuture.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Future
{
    public interface IFuture : IDisposable
    {
        public PID Pid { get; }
        public Task<object> Task { get; }

        public Task<object> GetTask(CancellationToken cancellationToken);
    }
}
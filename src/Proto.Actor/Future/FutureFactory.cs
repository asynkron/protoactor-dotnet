// -----------------------------------------------------------------------
// <copyright file="FutureFactory.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;

namespace Proto.Future
{
    public sealed class FutureFactory
    {
        private readonly ActorSystem System;

        public FutureFactory(ActorSystem system, CancellationToken cancellationToken = default)
        {
            System = system;
            Future = new SharedFutureProcess(System, 10000);
            cancellationToken.Register(() => {
                    Future.Stop(Future.Pid);
                }
            );
        }

        private SharedFutureProcess Future { get; set; }

        // public IFuture GetHandle(CancellationToken ct) => SingleProcessHandle();

        public IFuture SingleProcessHandle()
        {
            var f = Future.TryCreateHandle();

            if (f == default)
            {
                var shared = new SharedFutureProcess(System, 10000);
                f = shared.TryCreateHandle();
                Future = shared;
            }

            return f!;
        }

        // private IFuture SharedHandle(CancellationToken ct)
        // {
        //     var process = Future.Value!;
        //     var future = process.TryCreateHandle(ct);
        //
        //     if (future != default) return future;
        //
        //     Future.Value = process = new SharedFutureProcess(System, 1000);
        //     return process.TryCreateHandle(ct)!;
        // }
    }
}
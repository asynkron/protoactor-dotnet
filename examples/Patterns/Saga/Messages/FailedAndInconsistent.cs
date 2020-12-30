// -----------------------------------------------------------------------
// <copyright file="FailedAndInconsistent.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto;

namespace Saga.Messages
{
    class FailedAndInconsistent : Result
    {
        public FailedAndInconsistent(PID pid) : base(pid)
        {
        }
    }
}
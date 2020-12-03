// -----------------------------------------------------------------------
// <copyright file="FailedButConsistentResult.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto;

namespace Saga.Messages
{
    internal class FailedButConsistentResult : Result
    {
        public FailedButConsistentResult(PID pid) : base(pid)
        {
        }
    }
}
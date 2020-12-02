// -----------------------------------------------------------------------
// <copyright file="UnknownResult.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto;

namespace Saga.Messages
{
    internal class UnknownResult : Result
    {
        public UnknownResult(PID pid) : base(pid)
        {
        }
    }
}
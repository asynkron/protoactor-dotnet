// -----------------------------------------------------------------------
// <copyright file="SuccessResult.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto;

namespace Saga.Messages
{
    internal class SuccessResult : Result
    {
        public SuccessResult(PID pid) : base(pid)
        {
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file="Result.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto;

namespace Saga.Messages
{
    internal class Result
    {
        public Result(PID pid)
        {
            Pid = pid;
        }

        public PID Pid { get; }
    }
}
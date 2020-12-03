// -----------------------------------------------------------------------
// <copyright file="Credit.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto;

namespace Saga.Messages
{
    internal class Credit : ChangeBalance
    {
        public Credit(decimal amount, PID replyTo) : base(amount, replyTo)
        {
        }
    }
}
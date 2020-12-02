// -----------------------------------------------------------------------
// <copyright file="Debit.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto;

namespace Saga.Messages
{
    internal class Debit : ChangeBalance
    {
        public Debit(decimal amount, PID replyTo) : base(amount, replyTo)
        {
        }
    }
}
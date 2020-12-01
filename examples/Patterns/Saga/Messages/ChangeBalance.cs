// -----------------------------------------------------------------------
// <copyright file="ChangeBalance.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto;

namespace Saga.Messages
{
    internal abstract class ChangeBalance
    {
        protected ChangeBalance(decimal amount, PID replyTo)
        {
            ReplyTo = replyTo;
            Amount = amount;
        }

        public PID ReplyTo { get; set; }
        public decimal Amount { get; set; }
    }
}
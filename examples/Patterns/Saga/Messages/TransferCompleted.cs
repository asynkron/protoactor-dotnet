// -----------------------------------------------------------------------
// <copyright file="TransferCompleted.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto;

namespace Saga.Messages
{
    internal class TransferCompleted
    {
        public TransferCompleted(PID from, decimal fromBalance, PID to, decimal toBalance)
        {
            From = from;
            FromBalance = fromBalance;
            To = to;
            ToBalance = toBalance;
        }

        public PID From { get; }
        public decimal FromBalance { get; }
        public PID To { get; }
        public decimal ToBalance { get; }

        public override string ToString() =>
            $"{base.ToString()}: {From.Id} balance is {FromBalance}, {To.Id} balance is {ToBalance}";
    }
}
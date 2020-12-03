// -----------------------------------------------------------------------
// <copyright file="TransferFailed.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Saga.Messages
{
    internal class TransferFailed
    {
        public TransferFailed(string reason)
        {
            Reason = reason;
        }

        public string Reason { get; }

        public override string ToString() => $"{base.ToString()}: {Reason}";
    }
}
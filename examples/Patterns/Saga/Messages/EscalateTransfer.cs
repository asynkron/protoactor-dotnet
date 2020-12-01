// -----------------------------------------------------------------------
// <copyright file="EscalateTransfer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Saga.Messages
{
    internal class EscalateTransfer
    {
        public EscalateTransfer(string message)
        {
            Message = message;
        }

        public string Message { get; }

        public override string ToString() => $"{base.ToString()}: {Message}";
    }
}
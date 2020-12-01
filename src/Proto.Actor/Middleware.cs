// -----------------------------------------------------------------------
// <copyright file="Middleware.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;

namespace Proto
{
    public static class Middleware
    {
        internal static Task Receive(IReceiverContext context, MessageEnvelope envelope) => context.Receive(envelope);

        internal static Task Sender(ISenderContext context, PID target, MessageEnvelope envelope)
        {
            target.SendUserMessage(context.System, envelope);
            return Task.CompletedTask;
        }
    }
}
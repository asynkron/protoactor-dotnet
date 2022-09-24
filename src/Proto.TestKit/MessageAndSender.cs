// -----------------------------------------------------------------------
// <copyright file="MessageAndSender.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.TestKit;

internal class MessageAndSender
{
    public MessageAndSender(ISenderContext context) : this(context.Sender, context.Message)
    {
    }

    private MessageAndSender(PID? sender, object? message)
    {
        Sender = sender;
        Message = message;
    }

    public object? Message { get; }
    public PID? Sender { get; }
}
// -----------------------------------------------------------------------
// <copyright file="DeadLetter.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Proto.Extensions;
using Proto.Mailbox;
using Proto.Metrics;

// ReSharper disable once CheckNamespace
namespace Proto;

/// <summary>
///     A wrapper for a message that could not be delivered to the original recipient. Such message is wrapped in
///     a <see cref="DeadLetterEvent{T}" /> by the <see cref="DeadLetterProcess" /> and forwarded
///     to the <see cref="EventStream{T}" />
/// </summary>
[PublicAPI]
public class DeadLetterEvent
{
    public DeadLetterEvent(PID pid, object message, PID? sender) : this(pid, message, sender, MessageHeader.Empty)
    {
    }

    public DeadLetterEvent(PID pid, object message, PID? sender, MessageHeader? header)
    {
        Pid = pid;
        Message = message;
        Sender = sender;
        Header = header ?? MessageHeader.Empty;
    }

    /// <summary>
    ///     The PID of the actor that was the original recipient of the message.
    /// </summary>
    public PID Pid { get; }

    /// <summary>
    ///     The message that could not be delivered to the original recipient.
    /// </summary>
    public object Message { get; }

    /// <summary>
    ///     Sender of the message.
    /// </summary>
    public PID? Sender { get; }

    /// <summary>
    ///     Headers of the message.
    /// </summary>
    public MessageHeader Header { get; }

    public override string ToString() =>
        $"DeadLetterEvent: [ Pid: {Pid}, Message: {Message.GetMessageTypeName()}:{Message}, Sender: {Sender}, Headers: {Header} ]";
}

/// <summary>
///     A process that receives messages, that cannot be handled by the original recipients e.g. because they have been
///     stopped.
///     The message is then forwarded to the <see cref="EventStream{T}" /> as a <see cref="DeadLetterEvent" />
/// </summary>
public class DeadLetterProcess : Process
{
    public DeadLetterProcess(ActorSystem system) : base(system)
    {
    }

    protected internal override void SendUserMessage(PID pid, object message)
    {
        var (msg, sender, header) = MessageEnvelope.Unwrap(message);

        if (System.Metrics.Enabled)
        {
            ActorMetrics.DeadletterCount.Add(1,
                new KeyValuePair<string, object?>("id", System.Id),
                new KeyValuePair<string, object?>("address", System.Address),
                new KeyValuePair<string, object?>("messagetype", msg.GetType().Name)
            );
        }

        System.EventStream.Publish(new DeadLetterEvent(pid, msg, sender, header));

        if (sender is null)
        {
            return;
        }

        System.Root.Send(sender, new DeadLetterResponse { Target = pid });
    }

    protected internal override void SendSystemMessage(PID pid, SystemMessage message)
    {
        if (System.Metrics.Enabled)
        {
            ActorMetrics.DeadletterCount.Add(1, new KeyValuePair<string, object?>("id", System.Id),
                new KeyValuePair<string, object?>("address", System.Address),
                new KeyValuePair<string, object?>("messagetype", message.GetMessageTypeName()));
        }

        //trying to watch a dead pid returns terminated, NotFound
        if (message is Watch watch)
        {
            System.Root.Send(watch.Watcher, new Terminated { Who = pid, Why = TerminatedReason.NotFound });
        }

        System.EventStream.Publish(new DeadLetterEvent(pid, message, null, null));
    }
}

#pragma warning disable RCS1194
public class DeadLetterException : Exception
#pragma warning restore RCS1194
{
    public DeadLetterException(PID pid) : base($"{pid} no longer exists")
    {
    }
}
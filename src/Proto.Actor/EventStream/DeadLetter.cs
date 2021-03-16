// -----------------------------------------------------------------------
// <copyright file="DeadLetter.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace

// ReSharper disable once CheckNamespace

using System;
using JetBrains.Annotations;
using Proto.Metrics;

namespace Proto
{
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

        public PID Pid { get; }
        public object Message { get; }
        public PID? Sender { get; }
        public MessageHeader Header { get; }
    }

    public class DeadLetterProcess : Process
    {
        public DeadLetterProcess(ActorSystem system) : base(system)
        {
        }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            System.Metrics.Get<ActorMetrics>().DeadletterCount.Inc(new[] {System.Id, System.Address, message.GetType().Name});
            var (msg, sender, header) = MessageEnvelope.Unwrap(message);
            System.EventStream.Publish(new DeadLetterEvent(pid, msg, sender, header));
            if (sender is null) return;

            System.Root.Send(sender, msg is PoisonPill
                ? new Terminated {Who = pid, Why = TerminatedReason.NotFound}
                : new DeadLetterResponse {Target = pid}
            );
        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            System.Metrics.Get<ActorMetrics>().DeadletterCount.Inc(new[] {System.Id, System.Address, message.GetType().Name});
            System.EventStream.Publish(new DeadLetterEvent(pid, message, null, null));
        }
    }

    public class DeadLetterException : Exception
    {
        public DeadLetterException(PID pid) : base($"{pid} no longer exists")
        {
        }
    }
}
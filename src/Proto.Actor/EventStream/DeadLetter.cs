// -----------------------------------------------------------------------
//   <copyright file="DeadLetter.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------
// ReSharper disable once CheckNamespace


// ReSharper disable once CheckNamespace

namespace Proto
{
    using System;
    using JetBrains.Annotations;

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
            var (msg, sender, header) = MessageEnvelope.Unwrap(message);
            System.EventStream.Publish(new DeadLetterEvent(pid, msg, sender, header));
            
            if (sender != null && !(msg is PoisonPill)) System.Root.Send(sender, new DeadLetterResponse {Target = pid});
        }

        protected internal override void SendSystemMessage(PID pid, object message)
            => System.EventStream.Publish(new DeadLetterEvent(pid, message, null, null));
    }

    public class DeadLetterException : Exception
    {
        public DeadLetterException(PID pid) : base($"{pid.ToShortString()} no longer exists")
        {
        }
    }
}
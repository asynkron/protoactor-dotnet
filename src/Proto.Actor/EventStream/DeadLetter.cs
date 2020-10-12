// -----------------------------------------------------------------------
//   <copyright file="DeadLetter.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------
// ReSharper disable once CheckNamespace

using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
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
        public MessageHeader Header { get;  }
    }

    public class DeadLetterProcess : Process
    {
        public DeadLetterProcess(ActorSystem system) : base(system)
        {
        }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            var (msg, sender, header) = MessageEnvelope.Unwrap(message);
            System.EventStream.Publish(new DeadLetterEvent(pid, msg, sender,header));
        }

        protected internal override void SendSystemMessage(PID pid, object message)
            => System.EventStream.Publish(new DeadLetterEvent(pid, message, null, null));
    }
}
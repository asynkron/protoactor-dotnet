// -----------------------------------------------------------------------
// <copyright file="DeadLetter.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using JetBrains.Annotations;
using Proto.Metrics;

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
        public MessageHeader Header { get; }

        public override string ToString()
            => $"DeadLetterEvent: [ Pid: {Pid}, Message: {Message.GetType()}:{Message}, Sender: {Sender}, Headers: {Header} ]";
    }

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
                    new("id", System.Id), new("address", System.Address),
                    new("messagetype", msg.GetType().Name)
                );
            }

            System.EventStream.Publish(new DeadLetterEvent(pid, msg, sender, header));
            if (sender is null) return;

            System.Root.Send(sender,new DeadLetterResponse {Target = pid});
        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            if (System.Metrics.Enabled)
                ActorMetrics.DeadletterCount.Add(1, new("id", System.Id), new("address", System.Address), new("messagetype", message.GetType().Name));

            //trying to watch a dead pid returns terminated, NotFound
            if (message is Watch watch)
            {
                System.Root.Send(watch.Watcher, new Terminated {Who = pid, Why = TerminatedReason.NotFound});
            }
            
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
// -----------------------------------------------------------------------
//   <copyright file="DeadLetter.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------
namespace Proto
{
    public class DeadLetterEvent
    {
        internal DeadLetterEvent(PID pid, object message, PID sender)
        {
            Pid = pid;
            Message = message;
            Sender = sender;
        }

        public PID Pid { get; }
        public object Message { get; }
        public PID Sender { get; }
    }

    public class DeadLetterProcess : Process
    {
        public static readonly DeadLetterProcess Instance = new DeadLetterProcess();

        protected internal override void SendUserMessage(PID pid, object message)
        {
            var (msg,sender, _) = MessageEnvelope.Unwrap(message);
            EventStream.Instance.Publish(new DeadLetterEvent(pid, msg, sender));
        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            EventStream.Instance.Publish(new DeadLetterEvent(pid, message, null));
        }
    }
}
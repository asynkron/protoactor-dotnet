// -----------------------------------------------------------------------
//   <copyright file="DeadLetter.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

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

        protected internal override async Task SendUserMessageAsync(PID pid, object message)
        {
            var (msg,sender, _) = MessageEnvelope.Unwrap(message);
            await EventStream.Instance.PublishAsync(new DeadLetterEvent(pid, msg, sender));
        }

        protected internal override async Task SendSystemMessageAsync(PID pid, object message)
        {
            await EventStream.Instance.PublishAsync(new DeadLetterEvent(pid, message, null));
        }
    }
}
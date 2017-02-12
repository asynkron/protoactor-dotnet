// -----------------------------------------------------------------------
//  <copyright file="DeadLetter.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

namespace Proto
{
    public class DeadLetterEvent
    {
        public DeadLetterEvent(PID pid, object message, PID sender)
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

        public override void SendUserMessage(PID pid, object message, PID sender)
        {
            EventStream.Instance.Publish(new DeadLetterEvent(pid, message, sender));
        }

        public override void SendSystemMessage(PID pid, object message)
        {
            EventStream.Instance.Publish(new DeadLetterEvent(pid, message, null));
        }
    }
}
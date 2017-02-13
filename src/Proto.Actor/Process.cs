// -----------------------------------------------------------------------
//  <copyright file="Process.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using Proto.Mailbox;

namespace Proto
{
    public abstract class Process
    {
        public abstract void SendUserMessage(PID pid, object message, PID sender);

        public virtual void Stop(PID pid)
        {
            SendSystemMessage(pid, new Stop());
        }

        public abstract void SendSystemMessage(PID pid, object message);
    }

    public class LocalProcess : Process
    {
        public IMailbox Mailbox { get; }
        public bool IsDead { get; private set; }

        public LocalProcess(IMailbox mailbox)
        {
            Mailbox = mailbox;
        }


        public override void SendUserMessage(PID pid, object message, PID sender)
        {
            if (sender != null)
            {
                Mailbox.PostUserMessage(new MessageSender(message, sender));
                return;
            }

            Mailbox.PostUserMessage(message);
        }

        public override void SendSystemMessage(PID pid, object message)
        {
            Mailbox.PostSystemMessage(message);
        }

        public override void Stop(PID pid)
        {
            base.Stop(pid);
            IsDead = true;
        }
    }
}
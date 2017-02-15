// -----------------------------------------------------------------------
//  <copyright file="Process.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading;
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
        private long _isDead;
        public IMailbox Mailbox { get; }

        internal bool IsDead
        {
            get { return Interlocked.Read(ref _isDead) == 1; }
            private set { Interlocked.Exchange(ref _isDead, value ? 1 : 0); }
        }

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
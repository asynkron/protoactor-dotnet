// -----------------------------------------------------------------------
//   <copyright file="Process.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using Proto.Mailbox;

namespace Proto
{
    public abstract class Process
    {
        protected internal abstract void SendUserMessage(PID pid, object message);

        public virtual void Stop(PID pid)
        {
            SendSystemMessage(pid, Proto.Stop.Instance);
        }

        protected internal abstract void SendSystemMessage(PID pid, object message);
    }

    public class ActorProcess : Process
    {
        private long _isDead;

        public ActorProcess(IMailbox mailbox)
        {
            Mailbox = mailbox;
        }

        public IMailbox Mailbox { get; }

        internal bool IsDead
        {
            get => Interlocked.Read(ref _isDead) == 1;
            private set => Interlocked.Exchange(ref _isDead, value ? 1 : 0);
        }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            Mailbox.PostUserMessage(message);
        }

        protected internal override void SendSystemMessage(PID pid, object message)
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
// -----------------------------------------------------------------------
//  <copyright file="Process.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto
{
    public abstract class Process
    {
        protected internal abstract Task SendUserMessage(PID pid, object message);

        public virtual void Stop(PID pid)
        {
            SendSystemMessage(pid, new Stop());
        }

        protected internal abstract Task SendSystemMessage(PID pid, object message);
    }

    public class LocalProcess : Process
    {
        private long _isDead;
        public IMailbox Mailbox { get; }

        internal bool IsDead
        {
            get => Interlocked.Read(ref _isDead) == 1;
            private set => Interlocked.Exchange(ref _isDead, value ? 1 : 0);
        }

        public LocalProcess(IMailbox mailbox)
        {
            Mailbox = mailbox;
        }


        protected internal override Task SendUserMessage(PID pid, object message)
        {
            return Mailbox.PostUserMessage(message);
        }

        protected internal override Task SendSystemMessage(PID pid, object message)
        {
            return Mailbox.PostSystemMessage(message);
        }

        public override void Stop(PID pid)
        {
            base.Stop(pid);
            IsDead = true;
        }
    }
}
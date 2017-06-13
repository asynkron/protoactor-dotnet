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
        public abstract Task SendUserMessageAsync(PID pid, object message);

        public virtual async Task StopAsync(PID pid)
        {
            await SendSystemMessageAsync(pid, new Stop());
        }

        public abstract Task SendSystemMessageAsync(PID pid, object message);
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


        public override Task SendUserMessageAsync(PID pid, object message)
        {
            Mailbox.PostUserMessage(message);
            return Actor.Done;
        }

        public override Task SendSystemMessageAsync(PID pid, object message)
        {
            Mailbox.PostSystemMessage(message);
            return Actor.Done;
        }

        public override async Task StopAsync(PID pid)
        {
            await base.StopAsync(pid);
            IsDead = true;
        }
    }
}
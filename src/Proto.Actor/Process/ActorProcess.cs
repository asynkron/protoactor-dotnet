// -----------------------------------------------------------------------
// <copyright file="ActorProcess.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using Proto.Context;
using Proto.Future;
using Proto.Mailbox;

// ReSharper disable once CheckNamespace
namespace Proto
{
    public class ActorProcess : Process
    {
        private long _isDead;

        public ActorProcess(ActorSystem system, IMailbox mailbox) : base(system) => Mailbox = mailbox;

        private IMailbox Mailbox { get; }

        internal bool IsDead {
            get => Interlocked.Read(ref _isDead) == 1;
            private set => Interlocked.Exchange(ref _isDead, value ? 1 : 0);
        }

        protected internal override void SendUserMessage(PID pid, object message) =>
            Mailbox.PostUserMessage(message);

        protected internal override void SendSystemMessage(PID pid, object message) =>
            Mailbox.PostSystemMessage(message);

        public override void Stop(PID pid)
        {
            base.Stop(pid);
            IsDead = true;
        }
    }
}
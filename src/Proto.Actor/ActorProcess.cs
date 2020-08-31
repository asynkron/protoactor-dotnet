using System.Threading;
using Proto.Mailbox;

namespace Proto
{
    public class ActorProcess : Process
    {
        private long _isDead;

        public ActorProcess(ActorSystem system, IMailbox mailbox) : base(system)
        {
            Mailbox = mailbox;
        }

        private IMailbox Mailbox { get; }

        internal bool IsDead
        {
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
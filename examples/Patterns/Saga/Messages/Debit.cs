using Proto;

namespace Saga.Messages
{
    internal class Debit : ChangeBalance {
        public Debit(decimal amount, PID replyTo) : base(amount, replyTo)
        {
        }
    }
}
using Proto;

namespace Saga.Messages
{
    internal class Credit : ChangeBalance {
        public Credit(decimal amount, PID replyTo) : base(amount, replyTo)
        {
        }
    }
}
using Proto;

namespace Saga.Messages
{
    internal abstract class ChangeBalance
    {
        public PID ReplyTo { get; set; }
        public decimal Amount { get; set; }

        protected ChangeBalance(decimal amount, PID replyTo)
        {
            ReplyTo = replyTo;
            Amount = amount;
        }
    }
}
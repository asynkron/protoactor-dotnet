using Proto;

namespace Saga.Messages
{
    internal class TransferCompleted
    {
        public PID From { get; }
        public decimal FromBalance { get; }
        public PID To { get; }
        public decimal ToBalance { get; }

        public TransferCompleted(PID from, decimal fromBalance, PID to, decimal toBalance)
        {
            From = @from;
            FromBalance = fromBalance;
            To = to;
            ToBalance = toBalance;
        }

        public override string ToString()
        {
            return $"{base.ToString()}: {From.Id} balance is {FromBalance}, {To.Id} balance is {ToBalance}";
        }
    }
}
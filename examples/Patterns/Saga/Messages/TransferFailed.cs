namespace Saga.Messages
{
    internal class TransferFailed
    {
        public string Reason { get; }

        public TransferFailed(string reason)
        {
            Reason = reason;
        }

        public override string ToString()
        {
            return $"{base.ToString()}: {Reason}";
        }
    }
}
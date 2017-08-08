namespace Saga.Messages
{
    internal class EscalateTransfer
    {
        public string Message { get; }

        public EscalateTransfer(string message)
        {
            Message = message;
        }

        public override string ToString()
        {
            return $"{base.ToString()}: {Message}";
        }
    }
}
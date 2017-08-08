using Proto;

namespace Saga.Messages
{
    internal class UnknownResult : Result
    {
        public UnknownResult(PID pid) : base(pid)
        {
            
        }
    }
}
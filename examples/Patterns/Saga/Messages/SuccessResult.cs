using Proto;

namespace Saga.Messages
{
    internal class SuccessResult : Result
    {
        public SuccessResult(PID pid) : base(pid)
        {
        }
    }
}
using Proto;

namespace Saga.Messages
{
    internal class FailedAndInconsistent : Result {
        public FailedAndInconsistent(PID pid) : base(pid)
        {
        }
    }
}
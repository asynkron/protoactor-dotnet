using Proto;

namespace Saga.Messages
{
    internal class FailedButConsistentResult : Result {
        public FailedButConsistentResult(PID pid) : base(pid)
        {
        }
    }
}
using Proto;

namespace Saga.Messages
{
    internal class Result
    {
        public PID Pid { get; }
        public Result(PID pid)
        {
            Pid = pid;
        }
    }
}
using Proto.Context;

namespace Proto.TestFixtures
{
    public class TestProcess : Process
    {
        public TestProcess(ActorSystem system) : base(system)
        {
        }

        protected override void SendUserMessage(PID pid, object message, IExecutionContext? ec=null)
        {
        }

        protected override void SendSystemMessage(PID pid, object message, IExecutionContext? ec=null)
        {
        }
    }
}
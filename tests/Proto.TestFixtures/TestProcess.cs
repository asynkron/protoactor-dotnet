using System.Threading.Tasks;

namespace Proto.TestFixtures
{
    public class TestProcess : Process
    {
        protected override Task SendUserMessage(PID pid, object message)
        {
            return Task.FromResult(0);
        }

        protected override Task SendSystemMessage(PID pid, object message)
        {
            return Task.FromResult(0);
        }
    }
}

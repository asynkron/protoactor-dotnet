using System.Threading.Tasks;

namespace Proto.TestFixtures
{
    public class TestProcess : Process
    {
        protected override void SendUserMessage(PID pid, object message)
        {
            return Actor.Done;
        }

        protected override void SendSystemMessage(PID pid, object message)
        {
            return Actor.Done;
        }
    }
}

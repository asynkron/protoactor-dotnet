using System.Threading.Tasks;

namespace Proto.TestFixtures
{
    public class TestProcess : Process
    {
        public override Task SendUserMessageAsync(PID pid, object message)
        {
            return Actor.Done;
        }

        public override Task SendSystemMessageAsync(PID pid, object message)
        {
            return Actor.Done;
        }
    }
}

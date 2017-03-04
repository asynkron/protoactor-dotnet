using System.Threading.Tasks;

namespace Proto.TestFixtures
{
    public class DoNothingActor : IActor
    {
        public Task ReceiveAsync(IContext context) => Actor.Done;
    }
}

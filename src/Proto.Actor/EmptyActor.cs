using System.Threading.Tasks;

namespace Proto
{
    internal class EmptyActor : IActor
    {
        private readonly Receive _receive;
        public EmptyActor(Receive receive) => _receive = receive;
        public Task ReceiveAsync(IContext context) => _receive(context);
    }
}
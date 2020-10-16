using System.Threading.Tasks;

namespace Proto
{
    //this is used when creating actors from a Func
    internal class FunctionActor : IActor
    {
        private readonly Receive _receive;

        public FunctionActor(Receive receive) => _receive = receive;

        public Task ReceiveAsync(IContext context) => _receive(context);
    }
}
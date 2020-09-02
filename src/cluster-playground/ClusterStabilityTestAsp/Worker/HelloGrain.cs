using System.Threading.Tasks;
using Messages;

namespace Worker
{
    public class HelloGrain : IHelloGrain
    {
        public Task<HelloResponse> SayHello(HelloRequest request) => Task.FromResult(new HelloResponse { Message = "" });
    }
}
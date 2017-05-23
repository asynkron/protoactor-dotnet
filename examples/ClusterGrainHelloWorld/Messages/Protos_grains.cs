using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Proto;
using Proto.Cluster;

namespace Messages
{
    public static class GrainFactory
    {
        internal static Func<IHello> _HelloFactory;

        public static void HelloFactory(Func<IHello> factory) => _HelloFactory = factory;

        public static HelloClient HelloClient(string id)
        {
            return new HelloClient(id);
        }
    }
    public interface IHello
    {
        Task<HelloResponse> SayHello(HelloRequest request);
    }

    public class HelloClient
    {
        private readonly string _id;

        public HelloClient(string id)
        {
            _id = id;
        }

        public async Task<HelloResponse> SayHello(HelloRequest request)
        {
            var pid = await Cluster.GetAsync(_id, "Hello");
            var gr = new GrainRequest
            {
                Method = "SayHello",
                MessageData = request.ToByteString()
            };
            var res = await pid.RequestAsync<object>(gr);
            if (res is GrainResponse grainResponse)
            {
                return HelloResponse.Parser.ParseFrom(grainResponse.MessageData);
            }
            if (res is GrainErrorResponse grainErrorResponse)
            {
                throw new Exception(grainErrorResponse.Err);
            }
            throw new NotSupportedException();
        }
    }

    public class HelloActor : IActor
    {
        private IHello _inner;

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                {
                    _inner = GrainFactory._HelloFactory();
                    break;
                }
                case GrainRequest request:
                {
                    switch (request.Method)
                    {
                        case "SayHello":
                        {
                            var r = HelloRequest.Parser.ParseFrom(request.MessageData);
                            try
                            {
                                var res = await _inner.SayHello(r);
                                var grainResponse = new GrainResponse
                                {
                                    MessageData = res.ToByteString(),
                                };
                                context.Respond(grainResponse);
                            }
                            catch (Exception x)
                            {
                                var grainErrorResponse = new GrainErrorResponse
                                {
                                    Err = x.ToString()
                                };
                                context.Respond(grainErrorResponse);
                            }

                            break;
                        }
                    }

                    break;
                }
            }
        }
    }
}

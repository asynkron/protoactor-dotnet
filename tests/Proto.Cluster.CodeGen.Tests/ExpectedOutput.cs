#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Proto;
using Proto.Cluster;

namespace Acme.OtherSystem.Foo
{
    public static partial class GrainExtensions
    {
        public static TestGrainClient GetTestGrain(this Cluster cluster, string identity) => new TestGrainClient(cluster, identity);

        public static TestGrainClient GetTestGrain(this IContext context, string identity) => new TestGrainClient(context.System.Cluster(), identity);
    }

    public abstract class TestGrainBase
    {
        protected IContext Context {get;}
        protected ActorSystem System => Context.System;
        protected Cluster Cluster => Context.System.Cluster();
    
        protected TestGrainBase(IContext context)
        {
            Context = context;
        }
        
        public virtual Task OnStarted() => Task.CompletedTask;
        public virtual Task OnStopping() => Task.CompletedTask;
        public virtual Task OnStopped() => Task.CompletedTask;
        public virtual Task OnReceive() => Task.CompletedTask;

        public virtual async Task GetState(Action<Acme.Mysystem.Bar.GetCurrentStateResponse> respond, Action<string> onError)
        {
            try
            {
                var res = await GetState();
                respond(res);
            }
            catch (Exception x)
            {
                onError(x.ToString());
            }
        }
        public virtual async Task SendCommand(Acme.Mysystem.Bar.SomeCommand request, Action respond, Action<string> onError)
        {
            try
            {
                await SendCommand(request);
                respond();
            }
            catch (Exception x)
            {
                onError(x.ToString());
            }
        }
        public virtual async Task RequestResponse(Acme.Mysystem.Bar.Query request, Action<Acme.Mysystem.Bar.Response> respond, Action<string> onError)
        {
            try
            {
                var res = await RequestResponse(request);
                respond(res);
            }
            catch (Exception x)
            {
                onError(x.ToString());
            }
        }
        public virtual async Task NoParameterOrReturn(Action respond, Action<string> onError)
        {
            try
            {
                await NoParameterOrReturn();
                respond();
            }
            catch (Exception x)
            {
                onError(x.ToString());
            }
        }
    
        public abstract Task<Acme.Mysystem.Bar.GetCurrentStateResponse> GetState();
        public abstract Task SendCommand(Acme.Mysystem.Bar.SomeCommand request);
        public abstract Task<Acme.Mysystem.Bar.Response> RequestResponse(Acme.Mysystem.Bar.Query request);
        public abstract Task NoParameterOrReturn();
    }

    public class TestGrainClient
    {
        private readonly string _id;
        private readonly Cluster _cluster;

        public TestGrainClient(Cluster cluster, string id)
        {
            _id = id;
            _cluster = cluster;
        }

        public async Task<Acme.Mysystem.Bar.GetCurrentStateResponse?> GetState(CancellationToken ct)
        {
            var gr = new GrainRequestMessage(0, null);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, TestGrainActor.Kind, gr, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => (Acme.Mysystem.Bar.GetCurrentStateResponse?)grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException($"Unknown response type {res.GetType().FullName}")
            };
        }
        
        public async Task<Acme.Mysystem.Bar.GetCurrentStateResponse?> GetState(ISenderContext context, CancellationToken ct)
        {
            var gr = new GrainRequestMessage(0, null);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, TestGrainActor.Kind, gr,context, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => (Acme.Mysystem.Bar.GetCurrentStateResponse?)grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException($"Unknown response type {res.GetType().FullName}")
            };
        }
        public async Task<Google.Protobuf.WellKnownTypes.Empty?> SendCommand(Acme.Mysystem.Bar.SomeCommand request, CancellationToken ct)
        {
            var gr = new GrainRequestMessage(1, request);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, TestGrainActor.Kind, gr, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => Nothing.Instance,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException($"Unknown response type {res.GetType().FullName}")
            };
        }
        
        public async Task<Google.Protobuf.WellKnownTypes.Empty?> SendCommand(Acme.Mysystem.Bar.SomeCommand request, ISenderContext context, CancellationToken ct)
        {
            var gr = new GrainRequestMessage(1, request);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, TestGrainActor.Kind, gr,context, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => Nothing.Instance,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException($"Unknown response type {res.GetType().FullName}")
            };
        }
        public async Task<Acme.Mysystem.Bar.Response?> RequestResponse(Acme.Mysystem.Bar.Query request, CancellationToken ct)
        {
            var gr = new GrainRequestMessage(2, request);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, TestGrainActor.Kind, gr, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => (Acme.Mysystem.Bar.Response?)grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException($"Unknown response type {res.GetType().FullName}")
            };
        }
        
        public async Task<Acme.Mysystem.Bar.Response?> RequestResponse(Acme.Mysystem.Bar.Query request, ISenderContext context, CancellationToken ct)
        {
            var gr = new GrainRequestMessage(2, request);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, TestGrainActor.Kind, gr,context, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => (Acme.Mysystem.Bar.Response?)grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException($"Unknown response type {res.GetType().FullName}")
            };
        }
        public async Task<Google.Protobuf.WellKnownTypes.Empty?> NoParameterOrReturn(CancellationToken ct)
        {
            var gr = new GrainRequestMessage(3, null);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, TestGrainActor.Kind, gr, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => Nothing.Instance,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException($"Unknown response type {res.GetType().FullName}")
            };
        }
        
        public async Task<Google.Protobuf.WellKnownTypes.Empty?> NoParameterOrReturn(ISenderContext context, CancellationToken ct)
        {
            var gr = new GrainRequestMessage(3, null);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, TestGrainActor.Kind, gr,context, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => Nothing.Instance,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException($"Unknown response type {res.GetType().FullName}")
            };
        }
    }

    public class TestGrainActor : IActor
    {
        public const string Kind = "TestGrain";

        private TestGrainBase? _inner;
        private IContext? _context;
        private readonly Func<IContext, ClusterIdentity, TestGrainBase> _innerFactory;
    
        public TestGrainActor(Func<IContext, ClusterIdentity, TestGrainBase> innerFactory)
        {
            _innerFactory = innerFactory;
        }

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started msg: 
                {
                    _context = context;
                    var id = context.Get<ClusterIdentity>()!; // Always populated on startup
                    _inner = _innerFactory(context, id);
                    await _inner.OnStarted();
                    break;
                }
#pragma warning disable 618
                case ClusterInit _:
#pragma warning restore 618
                    //Ignored
                    break;
                case Stopping _:
                {
                    await _inner!.OnStopping();
                    break;
                }
                case Stopped _:
                {
                    await _inner!.OnStopped();
                    break;
                }    
                case GrainRequestMessage(var methodIndex, var r):
                {
                    switch (methodIndex)
                    {
                        case 0:
                        {   
                            await _inner!.GetState(Respond, OnError);

                            break;
                        }
                        case 1:
                        {   
                            if(r is Acme.Mysystem.Bar.SomeCommand input){
                                await _inner!.SendCommand(input, Respond, OnError);
                            } else {
                                OnError($"Invalid client contract. Expected Acme.Mysystem.Bar.SomeCommand, received {r?.GetType().FullName}");
                            }

                            break;
                        }
                        case 2:
                        {   
                            if(r is Acme.Mysystem.Bar.Query input){
                                await _inner!.RequestResponse(input, Respond, OnError);
                            } else {
                                OnError($"Invalid client contract. Expected Acme.Mysystem.Bar.Query, received {r?.GetType().FullName}");
                            }

                            break;
                        }
                        case 3:
                        {   
                            await _inner!.NoParameterOrReturn(Respond, OnError);

                            break;
                        }
                        default:
                            OnError($"Invalid client contract. Unexpected Index {methodIndex}");
                            break;
                    }

                    break;
                }
                default:
                {
                    await _inner!.OnReceive();
                    break;
                }
            }
        }

        private void Respond<T>(T response) where T: IMessage => _context!.Respond( new GrainResponseMessage(response));
        private void Respond() => _context!.Respond( new GrainResponseMessage(null));
        private void OnError(string error) => _context!.Respond( new GrainErrorResponse {Err = error } );

        public static ClusterKind GetClusterKind(Func<IContext, ClusterIdentity, TestGrainBase> innerFactory)
            => new ClusterKind(Kind, Props.FromProducer(() => new TestGrainActor(innerFactory)));
    }
}
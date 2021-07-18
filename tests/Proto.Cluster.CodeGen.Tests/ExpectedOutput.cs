using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Proto;
using Proto.Cluster;

namespace Acme.OtherSystem.Foo
{
    public static class GrainExtensions
    {
        public static TestGrainClient GetTestGrain(this Cluster cluster, string identity) => new(cluster, identity);

        public static TestGrainClient GetTestGrain(this IContext context, string identity) => new(context.System.Cluster(), identity);
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

        public async Task<Acme.Mysystem.Bar.GetCurrentStateResponse> GetState(CancellationToken ct)
        {
            var gr = new GrainRequestMessage(0, Nothing.Instance);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, "TestGrain", gr, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => (Acme.Mysystem.Bar.GetCurrentStateResponse)grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException("Unknown response type " + res.GetType().Name)
            };
        }
        
        public async Task<Acme.Mysystem.Bar.GetCurrentStateResponse> GetState(ISenderContext context, CancellationToken ct)
        {
            var gr = new GrainRequestMessage(0, request);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, "TestGrain", gr,context, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => (Acme.Mysystem.Bar.GetCurrentStateResponse)grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException("Unknown response type " + res.GetType().Name)
            };
        }
        public async Task<Google.Protobuf.WellKnownTypes.Empty> SendCommand(Acme.Mysystem.Bar.SomeCommand request, CancellationToken ct)
        {
            var gr = new GrainRequestMessage(1, request);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, "TestGrain", gr, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => (Google.Protobuf.WellKnownTypes.Empty)grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException("Unknown response type " + res.GetType().Name)
            };
        }
        
        public async Task<Google.Protobuf.WellKnownTypes.Empty> SendCommand(Acme.Mysystem.Bar.SomeCommand request, ISenderContext context, CancellationToken ct)
        {
            var gr = new GrainRequestMessage(1, request);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, "TestGrain", gr,context, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => (Google.Protobuf.WellKnownTypes.Empty)grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException("Unknown response type " + res.GetType().Name)
            };
        }
        public async Task<Acme.Mysystem.Bar.Response> RequestResponse(Acme.Mysystem.Bar.Query request, CancellationToken ct)
        {
            var gr = new GrainRequestMessage(2, request);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, "TestGrain", gr, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => (Acme.Mysystem.Bar.Response)grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException("Unknown response type " + res.GetType().Name)
            };
        }
        
        public async Task<Acme.Mysystem.Bar.Response> RequestResponse(Acme.Mysystem.Bar.Query request, ISenderContext context, CancellationToken ct)
        {
            var gr = new GrainRequestMessage(2, request);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, "TestGrain", gr,context, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => (Acme.Mysystem.Bar.Response)grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException("Unknown response type " + res.GetType().Name)
            };
        }
        public async Task<Google.Protobuf.WellKnownTypes.Empty> NoParameterOrReturn(CancellationToken ct)
        {
            var gr = new GrainRequestMessage(3, Nothing.Instance);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, "TestGrain", gr, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => (Google.Protobuf.WellKnownTypes.Empty)grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException("Unknown response type " + res.GetType().Name)
            };
        }
        
        public async Task<Google.Protobuf.WellKnownTypes.Empty> NoParameterOrReturn(ISenderContext context, CancellationToken ct)
        {
            var gr = new GrainRequestMessage(3, request);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, "TestGrain", gr,context, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => (Google.Protobuf.WellKnownTypes.Empty)grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException("Unknown response type " + res.GetType().Name)
            };
        }
    }

    class TestGrainActor : IActor
    {
        private TestGrainBase _inner;
        private IContext _context;
        private Func<IContext, string, string, TestGrainBase> _innerFactory;        
    
        public TestGrainActor(Func<IContext, string, string, TestGrainBase> innerFactory)
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
                    var id = context.Get<ClusterIdentity>();
                    _inner = _innerFactory(context, id.Identity, id.Kind);
                    await _inner.OnStarted();
                    break;
                }
                case ClusterInit:
                    //Ignored
                    break;
                case Stopping:
                {
                    await _inner.OnStopping();
                    break;
                }
                case Stopped:
                {
                    await _inner.OnStopped();
                    break;
                }    
                case GrainRequestMessage(var methodIndex, var r):
                {
                    switch (methodIndex)
                    {
                        case 0:
                        {   
                            if(r is Google.Protobuf.WellKnownTypes.Empty input){
                                await _inner.GetState(Nothing.Instance, Respond, OnError);
                            } else {
                                OnError("Invalid client contract");
                            }

                            break;
                        }
                        case 1:
                        {   
                            if(r is Acme.Mysystem.Bar.SomeCommand input){
                                await _inner.SendCommand(input, Respond, OnError);
                            } else {
                                OnError("Invalid client contract");
                            }

                            break;
                        }
                        case 2:
                        {   
                            if(r is Acme.Mysystem.Bar.Query input){
                                await _inner.RequestResponse(input, Respond, OnError);
                            } else {
                                OnError("Invalid client contract");
                            }

                            break;
                        }
                        case 3:
                        {   
                            if(r is Google.Protobuf.WellKnownTypes.Empty input){
                                await _inner.NoParameterOrReturn(Nothing.Instance, Respond, OnError);
                            } else {
                                OnError("Invalid client contract");
                            }

                            break;
                        }
                        default:
                            OnError("Invalid client contract");
                            break;
                    }

                    break;
                }
                default:
                {
                    await _inner.OnReceive();
                    break;
                }
            }
        }

        private void Respond<T>(T response) where T: IMessage => _context.Respond( new GrainResponseMessage(response));
        private void Respond() => _context.Respond( new GrainResponseMessage(Nothing.Instance));
        private void OnError(string error) => _context.Respond( new GrainErrorResponse {Err = error } );
    }
}
// -----------------------------------------------------------------------
// <copyright file="Template.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace ProtoBuf
{
    public static class Template
    {
        public const string DefaultTemplate = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Proto;
using Proto.Cluster;


namespace {{CsNamespace}}
{
    public static class Grains
    {
        public static class Factory<T>
        {
            public static Func<IContext,string,string,T> Create;
        }
        
        public static (string,Props)[] GetClusterKinds()  => new[] { 
            {{#each Services}}	
                (""{{Name}}"", Props.FromProducer(() => new {{Name}}Actor())),
            {{/each}}
            };
    }        
    
    public static class GrainExtensions
    {
        public static ClusterConfig With{{PackageName}}Kinds(this ClusterConfig config) => 
         config.WithClusterKinds(Grains.GetClusterKinds());
    
        {{#each Services}}
        public static {{Name}}Client Get{{Name}}(this Cluster cluster, string identity) => new(cluster, identity);
        {{/each}}
    }

	{{#each Services}}	
    public abstract class {{Name}}Base
    {
        protected IContext Context {get;}
    
        protected {{Name}}Base(IContext context)
        {
            Context = context;
        }
        
        public virtual Task OnStarted() => Task.CompletedTask;
        public virtual Task OnStopping() => Task.CompletedTask;
        public virtual Task OnStopped() => Task.CompletedTask;
        public virtual Task OnReceive() => Task.CompletedTask;

        {{#each Methods}}
        public virtual async Task {{Name}}({{InputName}} request, Action<{{OutputName}}> respond, Action<string> onError)
        {
            try
            {
                var res = await {{Name}}(request);
                respond(res);
            }
            catch (Exception x)
            {
                onError(x.ToString());
            }
        }
        {{/each}}
    
		{{#each Methods}}
        public abstract Task<{{OutputName}}> {{Name}}({{InputName}} request);
		{{/each}}
    }

    public class {{Name}}Client
    {
        private readonly string _id;
        private readonly Cluster _cluster;

        public {{Name}}Client(Cluster cluster, string id)
        {
            _id = id;
            _cluster = cluster;
        }

		{{#each Methods}}
        public async Task<{{OutputName}}> {{Name}}({{InputName}} request, CancellationToken ct)
        {
            var gr = new GrainRequestMessage({{Index}}, request);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, ""{{../Name}}"", gr, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => ({{OutputName}})grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                // unsupported response
                _ => throw new NotSupportedException()
            };
        }
		{{/each}}
    }

    class {{Name}}Actor : IActor
    {
        private {{Name}}Base _inner;
        private IContext _context;

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case ClusterInit msg: 
                {
                    _context = context;
                    _inner = Grains.Factory<{{Name}}Base>.Create(context, msg.Identity, msg.Kind);
                    await _inner.OnStarted();
                    break;
                }
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
			            {{#each Methods}}
                        case {{Index}}:
                        {   
                            if(r is {{InputName}} input){
                                await _inner.{{Name}}(input, Respond, OnError);
                            } else {
                                OnError(""Invalid client contract"");
                            }

                            break;
                        }
			            {{/each}}
                        default:
                            OnError(""Invalid client contract"");
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

        private void OnError(string error) => _context.Respond( new GrainErrorResponse {Err = error } );
    }
	{{/each}}	
}
";
    }
}
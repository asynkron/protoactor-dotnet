// -----------------------------------------------------------------------
// <copyright file="Template.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Cluster.CodeGen
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
    public static class GrainExtensions
    {
        {{#each Services}}
        public static {{Name}}Client Get{{Name}}(this Cluster cluster, string identity) => new(cluster, identity);

        public static {{Name}}Client Get{{Name}}(this IContext context, string identity) => new(context.System.Cluster(), identity);
        {{/each}}
    }

	{{#each Services}}	
    public abstract class {{Name}}Base
    {
        protected IContext Context {get;}
        protected ActorSystem System => Context.System;
        protected Cluster Cluster => Context.System.Cluster();
    
        protected {{Name}}Base(IContext context)
        {
            Context = context;
        }
        
        public virtual Task OnStarted() => Task.CompletedTask;
        public virtual Task OnStopping() => Task.CompletedTask;
        public virtual Task OnStopped() => Task.CompletedTask;
        public virtual Task OnReceive() => Task.CompletedTask;

        {{#each Methods}}
        public virtual async Task {{Name}}({{LeadingParameterDefinition}}Action{{#if UseReturn}}<{{OutputName}}>{{/if}} respond, Action<string> onError)
        {
            try
            {
                {{#if UseReturn}}
                var res = await {{Name}}({{Parameter}});
                respond(res);
                {{else}}
                await {{Name}}({{Parameter}});
                respond();
                {{/if}}
            }
            catch (Exception x)
            {
                onError(x.ToString());
            }
        }
        {{/each}}
    
		{{#each Methods}}
        public abstract Task{{#if UseReturn}}<{{OutputName}}>{{/if}} {{Name}}({{SingleParameterDefinition}});
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
        public async Task<{{OutputName}}> {{Name}}({{LeadingParameterDefinition}}CancellationToken ct)
        {
            var gr = new GrainRequestMessage({{Index}}, {{#if UseParameter}}{{Parameter}}{{else}}Nothing.Instance{{/if}});
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, ""{{../Name}}"", gr, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => ({{OutputName}})grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException(""Unknown response type "" + res.GetType().Name)
            };
        }
        
        public async Task<{{OutputName}}> {{Name}}({{LeadingParameterDefinition}}ISenderContext context, CancellationToken ct)
        {
            var gr = new GrainRequestMessage({{Index}}, request);
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, ""{{../Name}}"", gr,context, ct);

            return res switch
            {
                // normal response
                GrainResponseMessage grainResponse => ({{OutputName}})grainResponse.ResponseMessage,
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                //timeout
                null => null,
                // unsupported response
                _ => throw new NotSupportedException(""Unknown response type "" + res.GetType().Name)
            };
        }
		{{/each}}
    }

    class {{Name}}Actor : IActor
    {
        private {{Name}}Base _inner;
        private IContext _context;
        private Func<IContext, string, string, {{Name}}Base> _innerFactory;        
    
        public {{Name}}Actor(Func<IContext, string, string, {{Name}}Base> innerFactory)
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
			            {{#each Methods}}
                        case {{Index}}:
                        {   
                            if(r is {{InputName}} input){
                                await _inner.{{Name}}({{#if UseParameter}}input{{else}}Nothing.Instance{{/if}}, Respond, OnError);
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
        private void Respond() => _context.Respond( new GrainResponseMessage(Nothing.Instance));
        private void OnError(string error) => _context.Respond( new GrainErrorResponse {Err = error } );
    }
	{{/each}}	
}
";
    }
}
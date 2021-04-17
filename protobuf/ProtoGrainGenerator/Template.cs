// -----------------------------------------------------------------------
// <copyright file="Template.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace ProtoBuf
{
    public static class Template
    {
        public const string Code = @"
using System;
using System.Threading;
using System.Threading.Tasks;
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
        public static {{Name}}Client Get{{Name}}(this Proto.Cluster.Cluster cluster, string identity) => new(cluster, identity);
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
        
        Task OnStarted() => Task.CompletedTask;
        Task OnStopping() => Task.CompletedTask;
        Task OnStopped() => Task.CompletedTask;
        Task OnReceive() => Task.CompletedTask;
    
		{{#each Methods}}
        Task<{{OutputName}}> {{Name}}({{InputName}} request);
		{{/each}}
    }

    public class {{Name}}Client
    {
        private readonly string _id;
        private readonly Proto.Cluster.Cluster _cluster;

        public {{Name}}Client(Proto.Cluster.Cluster cluster, string id)
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

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case ClusterInit msg: 
                {
                    _inner = Grains.Factory<{{Name}}Base>.Create(context, msg.Identity, msg.Kind);
                    await _inner.OnStarted();
                    break;
                }
                case Proto.Stopping:
                {
                    await _inner.OnStopping();
                    break;
                }
                case Proto.Stopped:
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
                            try
                            {
                                var res = await _inner.{{Name}}(({{InputName}})r);
                                var response = new GrainResponseMessage(res);                                
                                context.Respond(response);
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
			            {{/each}}
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
    }
	{{/each}}	
}
";
    }
}
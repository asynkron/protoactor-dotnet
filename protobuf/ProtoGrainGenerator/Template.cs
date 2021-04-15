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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Messages;
using Proto;
using Proto.Cluster;

namespace {{CsNamespace}}
{
    public static class Grains
    {
        public static (string,Props)[] GetClusterKinds()  => new[] { 
            {{#each Services}}	
                (""{{Name}}"", Props.FromProducer(() => new {{Name}}Actor(null))),
            {{/each}}
            };
    }        
    
    
    public static class GrainExtensions
    {
        {{#each Services}}
        public static {{Name}}Client Get{{Name}}(this Proto.Cluster.Cluster cluster, string identity) => new(cluster, identity);
        {{/each}}
    }

	{{#each Services}}	
    public interface I{{Name}}
    {
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
            //request the RPC method to be invoked
            var res = await _cluster.RequestAsync<object>(_id, ""{{../Name}}"", gr, ct);

            return res switch
            {
                // normal response
                GrainResponse grainResponse => HelloResponse.Parser.ParseFrom(grainResponse.MessageData),
                // error response
                GrainErrorResponse grainErrorResponse => throw new Exception(grainErrorResponse.Err),
                // unsupported response
                _ => throw new NotSupportedException()
            };
        }
		{{/each}}
    }

    public class {{Name}}Actor : IActor
    {
        private I{{Name}} _inner;
        private readonly Grains _grains;

        public {{Name}}Actor(Grains grains) => _grains = grains;
        private string _identity;
        private string _kind;

        protected string Identity => _identity;
        protected string Kind => _kind;

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                {
                    _inner = _grains.Get{{Name}}(context.Self!.Id);
                    context.SetReceiveTimeout(TimeSpan.FromSeconds(30));
                    break;
                }
                case ClusterInit msg: 
                {
                    _identity = msg.Identity;
                    _kind = msg.Kind;
                    break;
                }
                case ReceiveTimeout _:
                {
                    context.Stop(context.Self!);
                    break;
                }
                case GrainRequest request:
                {
                    switch (request.MethodIndex)
                    {
						{{#each Methods}}
                        case {{Index}}:
                        {
                            var r = {{InputName}}.Parser.ParseFrom(request.MessageData);
                            try
                            {
                                var res = await _inner.{{Name}}(r);
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
						{{/each}}
                    }

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
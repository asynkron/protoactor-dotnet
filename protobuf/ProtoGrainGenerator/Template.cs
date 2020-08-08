// -----------------------------------------------------------------------
//   <copyright file="Template.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace ProtoBuf
{
    public static class Template
    {
        public const string Code = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Proto;
using Proto.Cluster;
using Proto.Remote;

namespace {{CsNamespace}}
{
    public class Grains
    {
        public Cluster Cluster { get; }

        public Grains(Cluster cluster) => Cluster = cluster;

		{{#each Services}}	
        internal Func<string, I{{Name}}> Get{{Name}} { get; private set; }

        public void {{Name}}Factory(Func<string, I{{Name}}> factory) 
        {
            Get{{Name}} = factory;
            Cluster.Remote.RegisterKnownKind(""{{Name}}"", Props.FromProducer(() => new {{Name}}Actor(this)));
        } 

        public void {{Name}}Factory(Func<I{{Name}}> factory) => {{Name}}Factory(id => factory());

        public {{Name}}Client {{Name}}(string id) => new {{Name}}Client(Cluster, id);
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
        private readonly Cluster _cluster;

        public {{Name}}Client(Cluster cluster, string id)
        {
            _id = id;
            _cluster = cluster;
        }

		{{#each Methods}}
        public Task<{{OutputName}}> {{Name}}({{InputName}} request) => {{Name}}(request, CancellationToken.None);

        public async Task<{{OutputName}}> {{Name}}({{InputName}} request, CancellationToken ct, GrainCallOptions options = null)
        {
            options ??= GrainCallOptions.Default;
            
            var gr = new GrainRequest
            {
                MethodIndex = {{Index}},
                MessageData = request.ToByteString()
            };

            async Task<{{OutputName}}> Inner() 
            {
                //resolve the grain
                var (pid, statusCode) = await _cluster.GetAsync(_id, ""{{../Name}}"", ct);

                if (statusCode != ResponseStatusCode.OK)
                {
                    throw new Exception($""Get PID failed with StatusCode: {statusCode}"");  
                }

                //request the RPC method to be invoked
                var res = await _cluster.System.Root.RequestAsync<object>(pid, gr, ct);

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

            for (int i = 0; i < options.RetryCount; i++)
            {
                try
                {
                    return await Inner();
                }
                catch (Exception)
                {
                    if (options.RetryAction != null)
                    {
                        await options.RetryAction(i);
                    }
                }
            }
            return await Inner();
        }
		{{/each}}
    }

    public class {{Name}}Actor : IActor
    {
        private I{{Name}} _inner;
        private readonly Grains _grains;

        public {{Name}}Actor(Grains grains) => _grains = grains;

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

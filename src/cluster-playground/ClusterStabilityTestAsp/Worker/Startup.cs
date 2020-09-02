using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proto;
using Proto.Remote;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Worker
{

    public class Startup
    {
        public Startup(IConfiguration configuration, IHostEnvironment hostingEnvironment)
        {
            Configuration = configuration;
            HostingEnvironment = hostingEnvironment;
        }

        public IConfiguration Configuration { get; }
        public IHostEnvironment HostingEnvironment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSeq(Configuration.GetSection("Seq"));
            });
            services.AddGrpc();
            services.AddRemote((remote, sp) =>
                {
                    remote.RemoteConfig.AdvertisedHostname = Environment.MachineName;
                    remote.RemoteConfig.AdvertisedPort = 80;
                    remote.Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
                    remote.RemoteKindRegistry.RegisterKnownKind("HelloActor", Props.FromProducer(() => ActivatorUtilities.GetServiceOrCreateInstance<HelloActor>(sp)));
                }
            );
            ConsulProviderOptions options = new ConsulProviderOptions
            {
                DeregisterCritical = TimeSpan.FromSeconds(2)
            };
            ConsulProvider clusterProvider = new ConsulProvider(options, c => { c.Address = new Uri("http://consul:8500"); });
            services.AddClustering(
                "StabilityTestAsp",
                clusterProvider, cluster =>
                {
                    var grains = cluster.AddGrains();
                    grains.HelloGrainFactory(() => new HelloGrain());
                    services.AddSingleton(grains);
                }
            );
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            Log.SetLoggerFactory(loggerFactory);
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/",
                        async context =>
                        {
                            await context.Response.WriteAsync(
                                "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909"
                            );
                        }
                    );
                }
            );
            app.UseProtoRemote();
        }
    }
}
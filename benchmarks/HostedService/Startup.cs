using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Identity;
using Proto.Cluster.Identity.MongoDb;
using Proto.Remote.GrpcCore;

namespace HostedService
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(l => l.AddConsole());
            Log.SetLoggerFactory(LoggerFactory.Create(l1 =>
                l1.AddConsole()
                    .SetMinimumLevel(LogLevel.Information)
            ));
            
            
            var settings = MongoClientSettings.FromUrl(MongoUrl.Create("mongodb://127.0.0.1:27017"));
            // settings.MinConnectionPoolSize = 10;
            // settings.MaxConnectionPoolSize = 100;
            settings.WaitQueueTimeout = TimeSpan.FromSeconds(10);
            settings.WaitQueueSize = 10000;
            
            var mongoClient = new MongoClient(settings);
            
            var pids = mongoClient.GetDatabase("dummydb").GetCollection<PidLookupEntity>("pids");

            var clusterProvider = new ConsulProvider(new ConsulProviderConfig());
            var identityLookup = new IdentityStorageLookup(new MongoIdentityStorage("foo", pids,150));
            var sys = new ActorSystem(new ActorSystemConfig().WithDeadLetterThrottleCount(3).WithDeadLetterThrottleInterval(TimeSpan.FromSeconds(1)))
                .WithRemote(GrpcCoreRemoteConfig.BindToLocalhost(9090))
                .WithCluster(ClusterConfig.Setup("test", clusterProvider, identityLookup)
                    .WithClusterKind("kind",Props.FromFunc(ctx => {
                        if (ctx.Message is int i)
                        {
                            ctx.Respond(i*2);
                        }
                        return Task.CompletedTask;
                    })));
            
            sys.Cluster().StartMemberAsync().Wait();

            services.AddSingleton(sys.Cluster());
            services.AddHostedService<ProtoHost>();


            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "HostedService v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}
using ActorMetrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using Proto.Metrics;

namespace WebApplication1
{
    public class Startup
    {
        public Startup(IConfiguration configuration) => Configuration = configuration;

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo {Title = "WebApplication1", Version = "v1"}); });

            services.AddOpenTelemetryMetrics(b =>
                b.AddMeter(ProtoMetrics.MeterName)
                    .AddView("protoactor_actor_messagereceive_duration", new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = new[] {.005, .01, .025, .05, .075, .1, .25, .5, .75, 1, 2.5, 5, 7.5, 10}
                        }
                    )
                    .AddPrometheusExporter(prom => prom.ScrapeResponseCacheDurationMilliseconds = 1000)
            );
            RunDummyCluster.Run();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebApplication1 v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseOpenTelemetryPrometheusScrapingEndpoint();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}
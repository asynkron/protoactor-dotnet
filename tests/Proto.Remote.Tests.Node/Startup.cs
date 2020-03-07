using Microsoft.Extensions.DependencyInjection;

namespace Proto.Remote.Tests.Node
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services) => services.AddHostedService<ProtoService>();

        // ReSharper disable once UnusedMember.Global
        public void Configure() { }
    }
}
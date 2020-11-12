using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.DependencyInjection;

namespace DependencyInjection.Controllers
{
    public class DependencyInjectedActor : IActor
    {
        private readonly ILogger<DependencyInjectedActor> _logger;

        public DependencyInjectedActor(ILogger<DependencyInjectedActor> logger)
        {
            //dependency injected arguments here
            _logger = logger;
        }
        
        public Task ReceiveAsync(IContext context) =>
            context.Message switch
            {
                HelloRequest msg => OnHelloMessage(msg,context),
                _                => Task.CompletedTask
            };

        private Task OnHelloMessage(HelloRequest msg, IContext context)
        {
            _logger.LogInformation("Got request");
            
            var greeting = $"Hello to you {msg.Name}";
            context.Respond(new HelloResponse(greeting));
            return Task.CompletedTask;
        }
    }

    public record HelloRequest(string Name);
    public record HelloResponse(string Greeting);
    
    [ApiController]
    [Route("[controller]")]
    public class ActorController : ControllerBase
    {
        private readonly ActorSystem _actorSystem;

        public ActorController(ActorSystem actorSystem)
        {
            _actorSystem = actorSystem;
        }

        [HttpGet]
        public async Task<string> Get()
        {
            //Get props for dependency injected actor 
            var props = _actorSystem.DI().PropsFor<DependencyInjectedActor>();
            
            //spawn the actor
            var pid = _actorSystem.Root.Spawn(props);

            //send a request and await the response
            var response = await _actorSystem.Root.RequestAsync<HelloResponse>(pid, new HelloRequest("Proto.Actor"));
            
            //stop the actor
            await _actorSystem.Root.StopAsync(pid);
            
            //return the result
            return response.Greeting;
        }
    }
}
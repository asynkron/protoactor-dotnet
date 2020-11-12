using Microsoft.AspNetCore.Mvc;
using Proto;

namespace DependencyInjection.Controllers
{
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
        public string Get()
        {
            //Use your actors here
            // _actorSystem.***
            
            return null;
        }
    }
}
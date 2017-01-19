using Proto.Router.Messages;
using Proto.Router.Routers;

namespace Proto.Router
{
    public class RouterActorRef : ActorRef
    {
        private readonly PID _router;
        private readonly RouterState _state;

        public RouterActorRef(PID router, RouterState state)
        {
            _router = router;
            _state = state;
        }

        public override void SendUserMessage(PID pid, object message, PID sender)
        {
            if (message is RouterManagementMessage)
            {
                var router = ProcessRegistry.Instance.Get(_router);
                router.SendUserMessage(pid, message, sender);
            }
            else
            {
                _state.RouteMessage(message, sender);
            }
        }

        public override void SendSystemMessage(PID pid, SystemMessage sys)
        {
            _router.SendSystemMessage(sys);
        }
    }
}
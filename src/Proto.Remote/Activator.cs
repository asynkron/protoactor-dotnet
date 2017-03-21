using System;
using System.Threading.Tasks;

namespace Proto.Remote
{
    public class Activator : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case ActorPidRequest msg:
                    var props = Remote.GetKnownKind(msg.Kind);
                    var name = msg.Name;
                    if (string.IsNullOrEmpty(name))
                    {
                        name = ProcessRegistry.Instance.NextId();
                    }
                    var pid = Actor.SpawnNamed(props, name);
                    var response = new ActorPidResponse
                    {
                        Pid = pid,
                    };
                    context.Respond(response);

                    break;
                default:
                    break;
            }
            return Actor.Done;
        }
    }
}
// -----------------------------------------------------------------------
//   <copyright file="Activator.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Proto.Remote
{
    public class Activator : IActor
    {
        private readonly ActorSystem _system;
        private readonly RemoteKindRegistry _remoteKindRegistry;
        
        public Activator(RemoteKindRegistry remoteKindRegistry, ActorSystem system)
        {
            _remoteKindRegistry = remoteKindRegistry;
            _system = system;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case ActorPidRequest msg:
                    var props = _remoteKindRegistry.GetKnownKind(msg.Kind);
                    var name = msg.Name;
                    if (string.IsNullOrEmpty(name))
                    {
                        name = _system.ProcessRegistry.NextId();
                    }

                    try
                    {
                        var pid = _system.Root.SpawnNamed(props, name);
                        var response = new ActorPidResponse {Pid = pid};
                        context.Respond(response);
                    }
                    catch (ProcessNameExistException ex)
                    {
                        var response = new ActorPidResponse
                        {
                            Pid = ex.Pid,
                            StatusCode = (int) ResponseStatusCode.ProcessNameAlreadyExist
                        };
                        context.Respond(response);
                    }
                    catch
                    {
                        var response = new ActorPidResponse
                        {
                            StatusCode = (int) ResponseStatusCode.Error
                        };
                        context.Respond(response);

                        throw;
                    }

                    break;
            }

            return Actor.Done;
        }
    }
}
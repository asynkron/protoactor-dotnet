// -----------------------------------------------------------------------
//   <copyright file="Activator.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Proto.Remote.Metrics;

namespace Proto.Remote
{
    public class Activator : IActor
    {
        private readonly RemoteConfigBase _remoteConfig;
        private readonly ActorSystem _system;

        public Activator(RemoteConfigBase remoteConfig, ActorSystem system)
        {
            _remoteConfig = remoteConfig;
            _system = system;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case ActorPidRequest msg:
                    var props = _remoteConfig.GetRemoteKind(msg.Kind);
                    var name = msg.Name;
                    if (string.IsNullOrEmpty(name)) name = _system.ProcessRegistry.NextId();

                    try
                    {
                        var pid = _system.Root.SpawnNamed(props, name);
                        context.System.Metrics.Get<RemoteMetrics>().RemoteActorSpawnCount.Inc(new[] {_system.Id, _system.Address, msg.Kind});
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

            return Task.CompletedTask;
        }
    }
}
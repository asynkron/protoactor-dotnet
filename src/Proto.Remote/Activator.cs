// -----------------------------------------------------------------------
//   <copyright file="Activator.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto.Remote
{
    public enum ActorPidRequestStatusCode
    {
        OK,
        Unavailable
    }

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

                    try
                    {
                        var pid = Actor.SpawnNamed(props, name);
                        var response = new ActorPidResponse
                        {
                            Pid = pid
                        };
                        context.Respond(response);
                    }
                    catch (ActivatorUnavailableException)
                    {
                        var response = new ActorPidResponse
                        {
                            StatusCode = (int) ActorPidRequestStatusCode.Unavailable
                        };
                        context.Respond(response);
                    }
                    catch (ActivatorCustomException ex)
                    {
                        var response = new ActorPidResponse
                        {
                            StatusCode = ex.Code
                        };
                        context.Respond(response);
                    }
                    break;
            }
            return Actor.Done;
        }
    }

    public class ActivatorUnavailableException : Exception { }

    public class ActivatorCustomException : Exception
    {
        public int Code { get; }
        public ActivatorCustomException(int code) => Code = code;
    }
}
// -----------------------------------------------------------------------
//   <copyright file="Activator.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.MongoIdentityLookup
{
    internal class MongoPlacementActor : IActor
    {
        private readonly Cluster _cluster;
        private readonly ILogger _logger;

        //pid -> the actor that we have created here
        //kind -> the actor kind
        //eventId -> the cluster wide eventId when this actor was created
        private readonly Dictionary<string, (PID pid, string kind)> _myActors =
            new Dictionary<string, (PID pid, string kind)>();

        private readonly Remote.Remote _remote;

        public MongoPlacementActor(Cluster cluster)
        {
            _cluster = cluster;
            _remote = _cluster.Remote;
            _logger = Log.CreateLogger($"{nameof(MongoPlacementActor)}-{cluster.LoggerId}");
        }

        public Task ReceiveAsync(IContext context)
        {
            return context.Message switch
                   {
                       Started _             => Started(context),
                       ReceiveTimeout _      => ReceiveTimeout(context),
                       Terminated msg        => Terminated(context, msg),
                       ActivationRequest msg => ActivationRequest(context, msg),
                       _                     => Actor.Done
                   };
        }

        private Task Started(IContext context)
        {
            context.SetReceiveTimeout(TimeSpan.FromSeconds(5));
            return Actor.Done;
        }

        private Task ReceiveTimeout(IContext context)
        {
            context.SetReceiveTimeout(TimeSpan.FromSeconds(5));
            var count = _myActors.Count;
            _logger.LogInformation("Statistics: Actor Count {ActorCount}", count);
            return Actor.Done;
        }

        private Task Terminated(IContext context, Terminated msg)
        {
            //TODO: if this turns out to be perf intensive, lets look at optimizations for reverse lookups
            var (identity, _) = _myActors.FirstOrDefault(kvp => kvp.Value.pid.Equals(msg.Who));

            _myActors.Remove(identity);
            return Actor.Done;
        }

        private Task ActivationRequest(IContext context, ActivationRequest msg)
        {
            var props = _remote.GetKnownKind(msg.Kind);
            var identity = msg.Identity;
            var kind = msg.Kind;
            try
            {
                if (_myActors.TryGetValue(identity, out var existing))
                {
                    //TODO: should we use identity+kind as key?

                    //this identity already exists
                    var response = new ActivationResponse
                    {
                        Pid = existing.pid
                    };
                    context.Respond(response);
                }
                else
                {
                    //this actor did not exist, lets spawn a new activation

                    //spawn and remember this actor
                    //as this id is unique for this activation (id+counter)
                    //we cannot get ProcessNameAlreadyExists exception here
                    var pid = context.SpawnPrefix(props, identity);

                    //give the grain knowledge of its grain name and kind
                    var grainInit = new GrainInit(identity, kind);

                    context.Send(pid, grainInit);
                    _myActors[identity] = (pid, kind);

                    var response = new ActivationResponse
                    {
                        Pid = pid
                    };
                    context.Respond(response);
                }
            }
            catch
            {
                var response = new ActivationResponse
                {
                    Pid = null
                };
                context.Respond(response);
            }

            return Actor.Done;
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file = "ShardActor.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto.Cluster.Sharding;


public delegate Props ShardEntityProducer(string shardId, string entityId, IContext parentContext);

public class ShardActor : IActor
{
    private ShardEntityProducer _propsFactory;
    private readonly Dictionary<string, PID> _childrenLookup;
    private readonly Dictionary<string, string> _childrenReverseLookup;
    
    //TODO: handle restarts, stop children, restart children etc.
    public ShardActor(ShardEntityProducer propsFactory)
    {
        _propsFactory = propsFactory;
        _childrenLookup = new();
        _childrenReverseLookup = new();
    }

    public Task ReceiveAsync(IContext context)
    {
        return context.Message switch
        {
            IShardMessage sm => OnShardMessage(sm, context),
            Terminated t     => OnChildTerminated(t, context),
            _                => Task.CompletedTask
        };
    }

    private Task OnChildTerminated(Terminated terminated, IContext context)
    {
        if (!_childrenReverseLookup.TryGetValue(terminated.Who.Id, out var key)) 
            return Task.CompletedTask;

        _childrenLookup.Remove(key);
        _childrenReverseLookup.Remove(terminated.Who.Id);

        return Task.CompletedTask;
    }

    private Task OnShardMessage(IShardMessage sm, IContext context)
    {
        var pid = EnsureEntityExists(context.ClusterIdentity()!.Identity, sm.EntityId, context);
        context.Forward(pid);
        return Task.CompletedTask;
    }

    private PID EnsureEntityExists(string shardId, string entityId, IContext context)
    {
        if (_childrenLookup.TryGetValue(entityId, out var pid))
            return pid;

        var props = _propsFactory(shardId, entityId, context);
        pid = context.SpawnNamed(props, entityId);
        
        _childrenLookup.Add(entityId, pid);
        _childrenReverseLookup.Add(pid.Id, entityId);

        return pid;
    }

    public static Props GetProps(ShardEntityProducer entityProducer) => Props.FromProducer(() => new ShardActor(entityProducer));
}
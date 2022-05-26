// -----------------------------------------------------------------------
// <copyright file = "ShardActor.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto.Cluster.Sharding;


public delegate Props ShardEntityProducer(string shardId, string entityId, IContext parentContext); 
public class ShardActor : IActor
{
    private ShardEntityProducer _propsFactory;
    private readonly Dictionary<string, PID> _children;
    
    //TODO: handle restarts, stop children, restart children etc.
    public ShardActor(ShardEntityProducer propsFactory)
    {
        _propsFactory = propsFactory;
        _children = new();
    }

    public Task ReceiveAsync(IContext context)
    {
        if (context.Message is IShardMessage sm)
        {
            return OnShardMessage(sm, context);
        }

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
        if (_children.TryGetValue(entityId, out var pid))
            return pid;

        var props = _propsFactory(shardId, entityId, context);
        pid = context.SpawnNamed(props, entityId);
        _children.Add(entityId, pid);

        return pid;
    }

    public static Props GetProps(ShardEntityProducer entityProducer) => Props.FromProducer(() => new ShardActor(entityProducer));
}
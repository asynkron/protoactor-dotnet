// -----------------------------------------------------------------------
// <copyright file="HelloActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;
using ClusterExperiment1.Messages;
using Proto;

namespace ClusterExperiment1;

public class WorkerActor : IActor
{
    // private readonly Random _rnd = new Random();

    public Task ReceiveAsync(IContext ctx)
    {
        switch (ctx.Message)
        {
            case Started _:
                //just to highlight when this happens
                break;
            case HelloRequest _:
                ctx.Respond(new HelloResponse());
                break;
        }

        return Task.CompletedTask;
    }
}
// -----------------------------------------------------------------------
// <copyright file="HelloActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using ClusterExperiment1.Messages;
using Proto;

namespace ClusterExperiment1
{
    public class WorkerActor : IActor
    {
        public Task ReceiveAsync(IContext ctx)
        {
            switch (ctx.Message)
            {
                case Started _:
                    //just to highlight when this happens
                    Console.Write("#");
                    break;
                case HelloRequest _:
                    ctx.Respond(new HelloResponse());
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
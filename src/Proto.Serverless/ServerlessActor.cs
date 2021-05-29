// -----------------------------------------------------------------------
// <copyright file="ServerlessActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Proto.Cluster;

namespace Proto.Serverless
{
    public class ServerlessActor : IActor
    {
        private string _kind;
        private string _identity;

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            Started _ => OnStarted(context),
            _ => Task.CompletedTask,
        };

        private Task OnStarted(IContext context)
        {
            var ci = context.Get<ClusterIdentity>();
            _identity = ci.Identity;
            _kind = ci.Kind;
            
            return Task.CompletedTask;
        }
    }
}
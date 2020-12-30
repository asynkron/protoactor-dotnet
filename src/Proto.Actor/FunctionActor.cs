// -----------------------------------------------------------------------
// <copyright file="FunctionActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;

namespace Proto
{
    //this is used when creating actors from a Func
    class FunctionActor : IActor
    {
        private readonly Receive _receive;

        public FunctionActor(Receive receive) => _receive = receive;

        public Task ReceiveAsync(IContext context) => _receive(context);
    }
}
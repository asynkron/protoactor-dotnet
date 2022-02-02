// -----------------------------------------------------------------------
// <copyright file="StashedMessage.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;

namespace Proto
{
    public record CapturedContext(MessageEnvelope MessageEnvelope, IContext Context){
        public async Task Receive()
        {
            var current = Context.Capture();
            await Context.Receive(MessageEnvelope);
            current.Apply();
        }

        public void Apply() => Context.Apply(this);
    }
}
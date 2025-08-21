// -----------------------------------------------------------------------
// <copyright file="EchoActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Proto;

namespace ActorTestKit;

public record Ping(string Who);
public record Pong(string Who);

// A simple actor that responds to Ping with Pong.
public class EchoActor : IActor
{
    public Task ReceiveAsync(IContext context)
    {
        if (context.Message is Ping ping)
        {
            context.Respond(new Pong(ping.Who));
        }

        return Task.CompletedTask;
    }
}

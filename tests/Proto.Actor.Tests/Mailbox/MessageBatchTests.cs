// -----------------------------------------------------------------------
// <copyright file="MessageBatchTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Proto.Mailbox.Tests
{
    public class MessageBatchTests
    {
        class MyMessageBatch : IMessageBatch
        {
            public IReadOnlyCollection<object> GetMessages() => new[] {"hello", "world", "batch"};
        }

        [Fact]
        public async Task CanUnpackMessagesFromBatchAndReceiveInOrder()
        {
            var batch = new MyMessageBatch();

            var messages = new List<object>();

            var system = new ActorSystem();
            var props = Props.FromFunc(ctx => {
                    if (ctx.Message is string str)
                    {
                        messages.Add(str);
                    }

                    return Task.CompletedTask;
                }
            );

            var pid = system.Root.Spawn(props);
            system.Root.Send(pid, batch);
            await system.Root.PoisonAsync(pid);

            var expected = new[] {"hello", "world", "batch"};
            messages.Should().ContainInOrder(expected);
        }
    }
}
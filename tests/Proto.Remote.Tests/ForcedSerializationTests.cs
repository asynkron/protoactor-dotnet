// -----------------------------------------------------------------------
// <copyright file = "ForcedSerializationTests.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ForcedSerialization.TestMessages;
using Proto;
using Proto.Remote;
using Xunit;
using MessageHeader = Proto.MessageHeader;

namespace Proto.Remote.Tests
{
    public class ForcedSerializationTests
    {
        private readonly Props _receivingActorProps;
        private readonly Props _sendingActorProps;
        private readonly ManualResetEvent _wait = new(false);
        private Proto.MessageHeader _header;
        private object _receivedMessage;
        private PID _sender;

        public ForcedSerializationTests()
        {
            _receivingActorProps = Props.FromFunc(ctx =>
                {
                    if (ctx.Message is TestMessage or TestRootSerializableMessage)
                    {
                        _receivedMessage = ctx.Message;
                        _sender = ctx.Sender;
                        _header = ctx.Headers;
                        ctx.Respond(new TestResponse());
                        _wait.Set();
                    }

                    return Task.CompletedTask;
                }
            );

            _sendingActorProps = Props.FromFunc(ctx =>
                    {
                        switch (ctx.Message)
                        {
                            case RunRequestAsync msg:
                                _ = ctx.RequestWithHeadersAsync<TestResponse>(msg.Target,
                                    new TestMessage("From another actor"), msg.Headers);

                                break;
                            case RunRequest msg:
                                ctx.Request(msg.Target, new TestMessage("From another actor"));

                                break;
                        }

                        return Task.CompletedTask;
                    }
                )
                .WithSenderMiddleware(ForcedSerializationSenderMiddleware.Create());
        }

        [Fact]
        public void The_test_messages_are_allowed_by_the_default_predicate()
        {
            var predicate = ForcedSerializationSenderMiddleware.SkipInternalProtoMessages;

            predicate(Proto.MessageEnvelope.Wrap(new TestMessage("test"))).Should().BeTrue();
            predicate(Proto.MessageEnvelope.Wrap(new TestRootSerializableMessage("test"))).Should().BeTrue();
        }

        [Fact]
        public void Sample_internal_proto_messages_are_not_allowed_by_the_default_predicate()
        {
            var predicate = ForcedSerializationSenderMiddleware.SkipInternalProtoMessages;

            predicate(Proto.MessageEnvelope.Wrap(Started.Instance)).Should().BeFalse();
            predicate(Proto.MessageEnvelope.Wrap(new RemoteDeliver(null!, null!, null!, null))).Should().BeFalse();
        }

        [Fact]
        public void It_serializes_and_deserializes()
        {
            var system = new ActorSystem(ActorSystemConfig.Setup()
                .WithConfigureRootContext(ctx => ctx.WithSenderMiddleware(
                        ForcedSerializationSenderMiddleware.Create()
                    )
                )
            );

            system.Extensions.Register(new Serialization());

            var pid = system.Root.Spawn(_receivingActorProps);
            var sentMessage = new TestMessage("Serialized");
            system.Root.Send(pid, sentMessage);

            _wait.WaitOne(TimeSpan.FromSeconds(2));

            _receivedMessage.Should()
                .BeEquivalentTo(sentMessage, "the received message should be the same as the sent message");

            _receivedMessage.Should().NotBeSameAs(sentMessage, "the message should have been serialized");
        }

        [Fact]
        public void It_should_not_serialize_if_predicate_prevents_it()
        {
            var system = new ActorSystem(ActorSystemConfig.Setup()
                .WithConfigureRootContext(ctx => ctx.WithSenderMiddleware(
                        ForcedSerializationSenderMiddleware.Create(_ => false)
                    )
                )
            );

            system.Extensions.Register(new Serialization());

            var pid = system.Root.Spawn(_receivingActorProps);
            var sentMessage = new TestMessage("Not serialized");
            system.Root.Send(pid, sentMessage);

            _wait.WaitOne(TimeSpan.FromSeconds(2));

            _receivedMessage.Should()
                .BeEquivalentTo(sentMessage, "the received message should be the same as the sent message");

            _receivedMessage.Should().BeSameAs(sentMessage, "the message should not have been serialized");
        }

        [Fact]
        public async Task It_preserves_headers()
        {
            var system = new ActorSystem(ActorSystemConfig.Setup());
            await using var _ = system;
            system.Extensions.Register(new Serialization());

            var pid = system.Root.Spawn(_receivingActorProps);
            var sender = system.Root.Spawn(_sendingActorProps);

            var headers = new Proto.MessageHeader(new Dictionary<string, string> { { "key", "value" } });
            system.Root.Send(sender, new RunRequestAsync(pid, headers));

            _wait.WaitOne(TimeSpan.FromSeconds(2));

            _header.Should().BeEquivalentTo(headers);
        }

        [Fact]
        public async Task It_preserves_sender()
        {
            var system = new ActorSystem(ActorSystemConfig.Setup());
            await using var _ = system;
            system.Extensions.Register(new Serialization());

            var pid = system.Root.Spawn(_receivingActorProps);
            var sender = system.Root.Spawn(_sendingActorProps);

            system.Root.Send(sender, new RunRequest(pid, null));

            _wait.WaitOne(TimeSpan.FromSeconds(2));

            _sender.Should().BeEquivalentTo(sender);
        }

        [Fact]
        public async Task It_can_handle_root_serializable()
        {
            var system = new ActorSystem(ActorSystemConfig.Setup()
                .WithConfigureRootContext(ctx => ctx.WithSenderMiddleware(
                        ForcedSerializationSenderMiddleware.Create()
                    )
                )
            );
            await using var _ = system;

            system.Extensions.Register(new Serialization());

            var pid = system.Root.Spawn(_receivingActorProps);
            var sentMessage = new TestRootSerializableMessage("Serialized");
            system.Root.Send(pid, sentMessage);

            _wait.WaitOne(TimeSpan.FromSeconds(2));

            _receivedMessage.Should()
                .BeEquivalentTo(sentMessage, "the received message should be the same as the sent message");

            _receivedMessage.Should().NotBeSameAs(sentMessage, "the message should have been serialized");
        }
    }
}

namespace ForcedSerialization.TestMessages
{
    internal record TestMessage(string Value);

    internal record TestRootSerializableMessage(string Value) : IRootSerializable
    {
        public IRootSerialized Serialize(ActorSystem system) => new TestRootSerializedMessage(Value);
    }

    internal record TestRootSerializedMessage(string Value) : IRootSerialized
    {
        public IRootSerializable Deserialize(ActorSystem system) => new TestRootSerializableMessage(Value);
    }

    internal record TestResponse;

    internal record RunRequest(PID Target, MessageHeader Headers);

    internal record RunRequestAsync(PID Target, MessageHeader Headers);
}
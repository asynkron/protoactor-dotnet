// -----------------------------------------------------------------------
// <copyright file = "ForcedSerializationTests.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using ForcedSerialization.TestMessages;
using Proto;
using Proto.Remote;
using Proto.TestKit;
using Xunit;

namespace Proto.Remote.Tests
{
    public class ForcedSerializationTests
    {
        private readonly Props _sendingActorProps;

        public ForcedSerializationTests()
        {
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

        private static Props CreateReceivingActorProps(PID probePid) =>
            Props.FromFunc(ctx =>
                {
                    if (ctx.Message is TestMessage or TestRootSerializableMessage)
                    {
                        ctx.Send(probePid, (ctx.Message, ctx.Sender, ctx.Headers));
                        ctx.Respond(new TestResponse());
                    }

                    return Task.CompletedTask;
                }
            );

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
        public async Task It_serializes_and_deserializes()
        {
            var system = new ActorSystem(ActorSystemConfig.Setup()
                .WithConfigureRootContext(ctx => ctx.WithSenderMiddleware(
                        ForcedSerializationSenderMiddleware.Create()
                    )
                )
            );
            await using var _ = system;

            system.Extensions.Register(new Serialization());

            var (probe, probePid) = system.CreateTestProbe();
            var pid = system.Root.Spawn(CreateReceivingActorProps(probePid));
            var sentMessage = new TestMessage("Serialized");
            system.Root.Send(pid, sentMessage);

            var (message, _, _) = await probe.GetNextMessageAsync<(object, PID, Proto.MessageHeader)>();

            message.Should()
                .BeEquivalentTo(sentMessage, "the received message should be the same as the sent message");

            message.Should().NotBeSameAs(sentMessage, "the message should have been serialized");
        }

        [Fact]
        public async Task It_should_not_serialize_if_predicate_prevents_it()
        {
            var system = new ActorSystem(ActorSystemConfig.Setup()
                .WithConfigureRootContext(ctx => ctx.WithSenderMiddleware(
                        ForcedSerializationSenderMiddleware.Create(_ => false)
                    )
                )
            );
            await using var _ = system;

            system.Extensions.Register(new Serialization());

            var (probe, probePid) = system.CreateTestProbe();
            var pid = system.Root.Spawn(CreateReceivingActorProps(probePid));
            var sentMessage = new TestMessage("Not serialized");
            system.Root.Send(pid, sentMessage);

            var (message, _, _) = await probe.GetNextMessageAsync<(object, PID, Proto.MessageHeader)>();

            message.Should()
                .BeEquivalentTo(sentMessage, "the received message should be the same as the sent message");

            message.Should().BeSameAs(sentMessage, "the message should not have been serialized");
        }

        [Fact]
        public async Task It_preserves_headers()
        {
            await using var system = new ActorSystem();
            system.Extensions.Register(new Serialization());

            var (probe, probePid) = system.CreateTestProbe();
            var pid = system.Root.Spawn(CreateReceivingActorProps(probePid));
            var sender = system.Root.Spawn(_sendingActorProps);

            var headers = new Proto.MessageHeader(new Dictionary<string, string> { { "key", "value" } });
            system.Root.Send(sender, new RunRequestAsync(pid, headers));

            var (_, _, receivedHeaders) = await probe.GetNextMessageAsync<(object, PID, Proto.MessageHeader)>();

            receivedHeaders.Should().BeEquivalentTo(headers);
        }

        [Fact]
        public async Task It_preserves_sender()
        {
            await using var system = new ActorSystem();
            system.Extensions.Register(new Serialization());

            var (probe, probePid) = system.CreateTestProbe();
            var pid = system.Root.Spawn(CreateReceivingActorProps(probePid));
            var sender = system.Root.Spawn(_sendingActorProps);

            system.Root.Send(sender, new RunRequest(pid, null));

            var (_, receivedSender, _) = await probe.GetNextMessageAsync<(object, PID, Proto.MessageHeader)>();

            receivedSender.Should().BeEquivalentTo(sender);
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

            var (probe, probePid) = system.CreateTestProbe();
            var pid = system.Root.Spawn(CreateReceivingActorProps(probePid));
            var sentMessage = new TestRootSerializableMessage("Serialized");
            system.Root.Send(pid, sentMessage);

            var (message, _, _) = await probe.GetNextMessageAsync<(object, PID, Proto.MessageHeader)>();

            message.Should()
                .BeEquivalentTo(sentMessage, "the received message should be the same as the sent message");

            message.Should().NotBeSameAs(sentMessage, "the message should have been serialized");
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

    internal record RunRequest(PID Target, Proto.MessageHeader Headers);

    internal record RunRequestAsync(PID Target, Proto.MessageHeader Headers);
}
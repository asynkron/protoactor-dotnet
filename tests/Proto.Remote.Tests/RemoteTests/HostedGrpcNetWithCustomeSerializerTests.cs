using System;
using System.Collections.Concurrent;
using Google.Protobuf;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using Xunit;

namespace Proto.Remote.Tests;

public class HostedGrpcNetWithCustomSerializerTests
    : RemoteTests,
        IClassFixture<HostedGrpcNetWithCustomSerializerTests.Fixture>
{
    public HostedGrpcNetWithCustomSerializerTests(Fixture fixture) : base(fixture)
    {
    }

    public class CustomSerializer : ISerializer
    {
        private readonly ConcurrentDictionary<string, Type> _types = new();

        public object Deserialize(ByteString bytes, string typeName)
        {
            var type = _types.GetOrAdd(typeName, name => Type.GetType(name));
            return System.Text.Json.JsonSerializer.Deserialize(bytes.ToStringUtf8(), type);
        }

        public string GetTypeName(object message) => message.GetType().AssemblyQualifiedName;

        public ByteString Serialize(object obj) =>
            ByteString.CopyFromUtf8(System.Text.Json.JsonSerializer.Serialize(obj));

        public bool CanSerialize(object obj) => true;
    }

    public class Fixture : RemoteFixture
    {
        public Fixture() : base(
            FixtureDescriptor(
                Client(RemoteTransportKind.HostedGrpcNet, ApplyCustomSerializer),
                Server(RemoteTransportKind.HostedGrpcNet, ApplyCustomSerializer),
                Server(RemoteTransportKind.HostedGrpcNet, ApplyCustomSerializer)
            )
        )
        {
        }

        private static GrpcNetRemoteConfig ApplyCustomSerializer(GrpcNetRemoteConfig config) =>
            config.WithSerializer(serializerId: 2, priority: 1000, new CustomSerializer());
    }
}

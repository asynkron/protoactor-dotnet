using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Hosting;
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

        public object Deserialize(ReadOnlySpan<byte> bytes, string typeName)
        {
            var type = _types.GetOrAdd(typeName, name => Type.GetType(name));
            return System.Text.Json.JsonSerializer.Deserialize(Encoding.UTF8.GetString(bytes), type);
        }

        public string GetTypeName(object message) => message.GetType().AssemblyQualifiedName;

        public ReadOnlySpan<byte> Serialize(object obj) =>
            Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(obj));

        public bool CanSerialize(object obj) => true;
    }

    public class Fixture : RemoteFixture
    {
        private readonly IHost _clientHost;
        private readonly IHost _serverHost;
        private readonly IHost _serverHost2;

        public Fixture()
        {
            var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost())
                .WithSerializer(serializerId: 2, priority: 1000, new CustomSerializer());
            (_clientHost, Remote) = GetHostedGrpcNetRemote(clientConfig);
            var serverConfig = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost())
                .WithSerializer(serializerId: 2, priority: 1000, new CustomSerializer());
            var serverConfig2 = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost())
                .WithSerializer(serializerId: 2, priority: 1000, new CustomSerializer());
            (_serverHost, ServerRemote1) = GetHostedGrpcNetRemote(serverConfig);
            (_serverHost2, ServerRemote2) = GetHostedGrpcNetRemote(serverConfig2);
        }

        public override async Task DisposeAsync()
        {
            await _clientHost.StopAsync();
            _clientHost.Dispose();
            await _serverHost.StopAsync();
            _serverHost.Dispose();
            await _serverHost2.StopAsync();
            _serverHost2.Dispose();
        }
    }
}
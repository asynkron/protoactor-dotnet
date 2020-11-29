using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Hosting;
using Proto.Remote.GrpcNet;
using Xunit;

namespace Proto.Remote.Tests
{
    public class HostedGrpcNetWithCustomSerializerTests : RemoteTests, IClassFixture<HostedGrpcNetWithCustomSerializerTests.Fixture>
    {
        public class CustomSerializer : ISerializer
        {
            private readonly ConcurrentDictionary<string, Type> _types = new ConcurrentDictionary<string, Type>();
            public object Deserialize(ByteString bytes, string typeName)
            {
                var type = _types.GetOrAdd(typeName, name => Type.GetType(name));
                return System.Text.Json.JsonSerializer.Deserialize(bytes.ToStringUtf8(), type);
            }

            public string GetTypeName(object message)
            {
                return message.GetType().AssemblyQualifiedName;
            }

            public ByteString Serialize(object obj)
            {
                return ByteString.CopyFromUtf8(System.Text.Json.JsonSerializer.Serialize(obj));
            }
        }
        public class Fixture : RemoteFixture
        {
            private readonly IHost _clientHost;
            private readonly IHost _serverHost;
            public Fixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost()).WithSerializer(new CustomSerializer(), true);
                (_clientHost, Remote) = GetHostedGrpcNetRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost()).WithSerializer(new CustomSerializer(), true);
                (_serverHost, ServerRemote) = GetHostedGrpcNetRemote(serverConfig);
            }
            public override async Task DisposeAsync()
            {
                await _clientHost.StopAsync();
                _clientHost.Dispose();
                await _serverHost.StopAsync();
                _serverHost.Dispose();
            }
        }
        public HostedGrpcNetWithCustomSerializerTests(Fixture fixture) : base(fixture)
        {
        }
    }
}
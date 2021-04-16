using System.Collections.Generic;
using System.Collections.Immutable;
using Proto.Remote.Tests.Messages;
using Xunit;

namespace Proto.Remote.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void CanSerializeAndDeserializeJsonPid()
        {
            var serialization = new Serialization();
            const string typeName = "actor.PID";
            var json = new JsonMessage(typeName, "{ \"Address\":\"123\", \"Id\":\"456\"}");
            var bytes = serialization.Serialize(json, 1);
            var deserialized = serialization.Deserialize(typeName, bytes, 1) as PID;
            Assert.NotNull(deserialized);
            Assert.Equal("123", deserialized.Address);
            Assert.Equal("456", deserialized.Id);
        }

        [Fact]
        public void CanSerializeAndDeserializeJson()
        {
            var serialization = new Serialization();
            serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
            const string typeName = "remote_test_messages.Ping";
            var json = new JsonMessage(typeName, "{ \"message\":\"Hello\"}");
            var bytes = serialization.Serialize(json, 1);
            var deserialized = serialization.Deserialize(typeName, bytes, 1) as Ping;
            Assert.NotNull(deserialized);
            Assert.Equal("Hello", deserialized.Message);
        }

        [Fact]
        public void CanSerializeAndDeserializeWithNetJsonSerializer()
        {
            var serialization = new Serialization();
            serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
            serialization.RegisterSerializer(s => new SystemTextJsonSerializer(s), true);

            var foo = new Foo
            {
                Value = 2,
                Bars = new List<Bar> { new Bar { Value = 3 }, new Bar { Value = 4 } }.ToImmutableList()
            };
            var bytes_Foo = serialization.Serialize(foo, serialization.DefaultSerializerId);
            var typeName_Foo = serialization.GetTypeName(foo, serialization.DefaultSerializerId);
            var deserialized_Foo = serialization.Deserialize(typeName_Foo, bytes_Foo, serialization.DefaultSerializerId) as Foo;
            Assert.NotNull(deserialized_Foo);
            Assert.Equal(2, deserialized_Foo.Value);
            Assert.Equal(2, deserialized_Foo.Bars.Count);
            Assert.Equal(3, deserialized_Foo.Bars[0].Value);
            Assert.Equal(4, deserialized_Foo.Bars[1].Value);

            var ping = new Ping() { Message = "FooBar" };
            var bytes_Ping = serialization.Serialize(ping, serialization.DefaultSerializerId);
            var typeName_Ping = serialization.GetTypeName(ping, serialization.DefaultSerializerId);
            var deserialized_Ping = serialization.Deserialize(typeName_Ping, bytes_Ping, serialization.DefaultSerializerId) as Ping;
            Assert.NotNull(deserialized_Ping);
            Assert.Equal("FooBar", deserialized_Ping.Message);
        }
    }
    public record Foo
    {
        public int Value { get; init; }
        public ImmutableList<Bar> Bars { get; init; }
    }
    public record Bar
    {
        public int Value { get; init; }
    }
}
using FluentAssertions;
using Google.Protobuf;
using Proto.Remote.Tests.Messages;
using Xunit;

namespace Proto.Remote.Tests;

public class SerializationTests
{
    [Fact]
    public void ProtobufDefaultValuesAreSameAsEmpty()
    {
        var p1 = new PID();

        var p2 = new PID
        {
            Address = "",
            Id = ""
        };

        var b1 = p1.ToByteArray();
        var b2 = p2.ToByteArray();
        b1.Length.Should().Be(b2.Length);
    }

    [Fact]
    public void CanUtilizeMultipleSerializers()
    {
        var serialization = new Serialization();

        serialization.RegisterSerializer(
            2,
            100,
            new MockSerializer1());

        serialization.RegisterSerializer(
            3,
            50,
            new MockSerializer2());

        // Check if we Serialization uses the MockSerializer1 when given the TestType1.
        {
            var (bytes, typeName, serializerId) = serialization.Serialize(new TestType1());
            Assert.Equal("MockSerializer1", typeName);
            Assert.Equal(2, serializerId);
            var deserialized = serialization.Deserialize(typeName, bytes, serializerId);
            Assert.True(deserialized is TestType1);
        }

        // Check if we Serialization uses the MockSerializer2 when given the TestType2.
        {
            var (bytes, typeName, serializerId) = serialization.Serialize(new TestType2());
            Assert.Equal("MockSerializer2", typeName);
            Assert.Equal(3, serializerId);
        }

        // Check if we fallback to JSON.
        {
            var json = new JsonMessage("Test", 10);
            var (bytes, typeName, serializerId) = serialization.Serialize(json);
            var deserialized = serialization.Deserialize(typeName, bytes, serializerId) as JsonMessage;
            Assert.NotNull(deserialized);
            Assert.Equal(json.Test, deserialized.Test);
            Assert.Equal(json.Test2, deserialized.Test2);
        }
    }

    [Fact]
    public void CanSerializeAndDeserializeJson()
    {
        var serialization = new Serialization();
        serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
        var json = new JsonMessage("Test", 10);
        var (bytes, typeName, serializerId) = serialization.Serialize(json);
        var deserialized = serialization.Deserialize(typeName, bytes, serializerId) as JsonMessage;
        Assert.NotNull(deserialized);
        Assert.Equal(json.Test, deserialized.Test);
        Assert.Equal(json.Test2, deserialized.Test2);
    }

    [Fact]
    public void CanSerializeBinaryMessage()
    {
        var serialization = new Serialization();
        serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);

        var msg = new BinaryMessage
        {
            Payload = ByteString.CopyFromUtf8("hello world")
        };

        var (bytes, typeName, serializerId) = serialization.Serialize(msg);
        var deserialized = serialization.Deserialize(typeName, bytes, serializerId) as BinaryMessage;
        deserialized!.Payload.Should().BeEquivalentTo(ByteString.CopyFromUtf8("hello world"));
    }

    public class TestType1
    {
    }

    public class TestType2
    {
    }

    public record JsonMessage(string Test, int Test2);

    public class MockSerializer1 : ISerializer
    {
        public bool CanSerialize(object obj) => obj is TestType1;

        public object Deserialize(ByteString bytes, string typeName) => new TestType1();

        public string GetTypeName(object message) => "MockSerializer1";

        public ByteString Serialize(object obj) => ByteString.CopyFrom();
    }

    public class MockSerializer2 : ISerializer
    {
        public bool CanSerialize(object obj) => obj is TestType1 || obj is TestType2;

        public object Deserialize(ByteString bytes, string typeName) => new TestType1();

        public string GetTypeName(object message) => "MockSerializer2";

        public ByteString Serialize(object obj) => ByteString.CopyFrom();
    }
}
using Google.Protobuf;
using Proto.Remote.Tests.Messages;
using Xunit;

namespace Proto.Remote.Tests
{
    public class SerializationTests
    {
        public class TestType1 { }
        public class TestType2 { }

        public class MockSerializer1 : ISerializer
        {
            public bool CanSerialize(object obj) => obj is TestType1;
            public object Deserialize(ByteString bytes, string typeName) => new TestType1();
            public string GetTypeName(object message) => $"MockSerializer1";
            public ByteString Serialize(object obj) => ByteString.CopyFrom(new byte[0]);
        }

        public class MockSerializer2 : ISerializer
        {
            public bool CanSerialize(object obj) => obj is TestType1 || obj is TestType2;
            public object Deserialize(ByteString bytes, string typeName) => new TestType1();
            public string GetTypeName(object message) => $"MockSerializer2";
            public ByteString Serialize(object obj) => ByteString.CopyFrom(new byte[0]);
        }

        [Fact]
        public void CanUtilizeMultipleSerializers()
        {
            var serialization = new Serialization();
            serialization.RegisterSerializer(
                serializerId: 2,
                priority: 100,
                serializer: new MockSerializer1());
            serialization.RegisterSerializer(
                serializerId: 3,
                priority: 50,
                serializer: new MockSerializer2());

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
                var json = new JsonMessage(
                    "actor.PID",
                    "{ \"Address\":\"123\", \"Id\":\"456\"}");
                var (bytes, typeName, serializerId) = serialization.Serialize(json);
                var deserialized = serialization.Deserialize(typeName, bytes, serializerId) as PID;
                Assert.NotNull(deserialized);
                Assert.Equal("123", deserialized.Address);
            }
        }

        [Fact]
        public void CanSerializeAndDeserializeJsonPid()
        {
            var serialization = new Serialization();
            var json = new JsonMessage(
                "actor.PID",
                "{ \"Address\":\"123\", \"Id\":\"456\"}");
            var (bytes, typeName, serializerId) = serialization.Serialize(json);
            var deserialized = serialization.Deserialize(typeName, bytes, serializerId) as PID;
            Assert.NotNull(deserialized);
            Assert.Equal("123", deserialized.Address);
            Assert.Equal("456", deserialized.Id);
        }

        [Fact]
        public void CanSerializeAndDeserializeJson()
        {
            var serialization = new Serialization();
            serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
            var json = new JsonMessage(
                "remote_test_messages.Ping",
                "{ \"message\":\"Hello\"}");
            var (bytes, typeName, serializerId) = serialization.Serialize(json);
            var deserialized = serialization.Deserialize(typeName, bytes, serializerId) as Ping;
            Assert.NotNull(deserialized);
            Assert.Equal("Hello", deserialized.Message);
        }
    }
}
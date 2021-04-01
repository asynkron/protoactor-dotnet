using Google.Protobuf;
using Proto.Remote.Tests.Messages;
using Xunit;

namespace Proto.Remote.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void CanSerializeAndDeserializeJsonPid()
        {
            Serialization serialization = new Serialization();
            const string typeName = "actor.PID";
            JsonMessage json = new JsonMessage(typeName, "{ \"Address\":\"123\", \"Id\":\"456\"}");
            ByteString bytes = serialization.Serialize(json, 1);
            PID deserialized = serialization.Deserialize(typeName, bytes, 1) as PID;
            Assert.NotNull(deserialized);
            Assert.Equal("123", deserialized.Address);
            Assert.Equal("456", deserialized.Id);
        }

        [Fact]
        public void CanSerializeAndDeserializeJson()
        {
            Serialization serialization = new Serialization();
            serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
            const string typeName = "remote_test_messages.Ping";
            JsonMessage json = new JsonMessage(typeName, "{ \"message\":\"Hello\"}");
            ByteString bytes = serialization.Serialize(json, 1);
            Ping deserialized = serialization.Deserialize(typeName, bytes, 1) as Ping;
            Assert.NotNull(deserialized);
            Assert.Equal("Hello", deserialized.Message);
        }
    }
}

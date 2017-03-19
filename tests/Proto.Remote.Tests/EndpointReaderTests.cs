using Proto.TestFixtures;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Remote.Tests
{
    public class EndpointReaderTests
    {
        [Fact]
        public async Task Given_EndpointReader_When_Receive_Empty_MessageBatchs_Then_Do_Nothing()
        {
            var sut = new EndpointReader();
            await sut.Receive(
                new TestAsyncStreamReader<MessageBatch>(new MessageBatch(), new MessageBatch()),
                null,   // not used 
                null    // not used
                );
        }
    }
}

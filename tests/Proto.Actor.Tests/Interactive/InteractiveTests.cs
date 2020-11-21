using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proto.Interactive;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Tests.Interactive
{
    public class InteractiveTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public InteractiveTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task CanBatchProcessIEnumerable()
        {
            var ints = Enumerable.Range(0, 100).ToList();
            var threads = new ConcurrentDictionary<Thread, bool>();
            
            await ints.ParallelForEach(i => threads.TryAdd(Thread.CurrentThread, true));
            var threadCount = threads.Count;
            
            //this is not really guaranteed to be true, in theory you could have a threadpool of 1 I suppose (?)
            Assert.True(threadCount > 1);
        }
    }
}
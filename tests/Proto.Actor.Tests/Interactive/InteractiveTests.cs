using System.Collections.Concurrent;
using System.Collections.Generic;
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

        public InteractiveTests(ITestOutputHelper testOutputHelper) => _testOutputHelper = testOutputHelper;

        [Fact]
        public async Task CanBatchProcessIEnumerable()
        {
            List<int> ints = Enumerable.Range(1, 100).ToList();
            ConcurrentDictionary<Thread, bool> threads = new ConcurrentDictionary<Thread, bool>();
            ConcurrentDictionary<int, bool> numbers = new ConcurrentDictionary<int, bool>();

            await ints.ParallelForEach(i =>
                {
                    threads.TryAdd(Thread.CurrentThread, true);
                    numbers.TryAdd(i, true);
                }
            );

            //this is not really guaranteed to be true, in theory you could have a threadpool of 1 I suppose (?)
            //    Assert.True(threads.Count > 1);
            Assert.Equal(100, numbers.Count);
        }
    }
}

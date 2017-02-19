using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Remote.Tests.Fixture
{
    public class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        readonly IEnumerator<T> _contentEnumerator;

        public TestAsyncStreamReader(params T[] content)
        {
            _contentEnumerator = content.ToList().GetEnumerator();
        }

        public T Current => _contentEnumerator.Current;

        public Task<bool> MoveNext(CancellationToken cancellationToken) => Task.FromResult(_contentEnumerator.MoveNext());

        public void Dispose() { }
    }
}

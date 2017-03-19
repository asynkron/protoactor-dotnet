using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Proto.TestFixtures
{
    public class TestServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public WriteOptions WriteOptions { get; set; }

        public Task WriteAsync(T message) => Task.CompletedTask;
    }
}

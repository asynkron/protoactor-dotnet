using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests
{
    public class LocalContextTests
    {
        class SupervisorStrategyMock : ISupervisorStrategy
        {
            public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics crs, Exception cause) { }
        }

        [Fact]
        public void Given__Context_ctor_should_set_some_fields()
        {
            var producer = (Func<IActor>)(() => null);
            var supervisorStrategyMock = new SupervisorStrategyMock();
            var middleware = new Receive(ctx => Task.CompletedTask);
            var parent = new PID("test", "test");

            var context = new Context(producer, supervisorStrategyMock, middleware, parent);

            Assert.Equal(parent, context.Parent);

            Assert.Null(context.Message);
            Assert.Null(context.Sender);
            Assert.Null(context.Self);
            Assert.Null(context.Actor);
            Assert.Null(context.Children);

            Assert.Equal(TimeSpan.Zero, context.ReceiveTimeout);
        }

      
    }
}

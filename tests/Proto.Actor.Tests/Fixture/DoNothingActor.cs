using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Proto.Tests.Fixture
{
    public class DoNothingActor : IActor
    {
        public Task ReceiveAsync(IContext context) => Actor.Done;
    }
}

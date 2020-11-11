using System.Threading.Tasks;
using Proto.Extensions;
using Xunit;

namespace Proto.Tests.Extensions
{
    public class ExtensionA : IActorSystemExtension<ExtensionA>
    {
        public int A { get; set; }
    }
    
    public class ExtensionB : IActorSystemExtension<ExtensionB>
    {
        public string B { get; set; }
    }
    
    public class ExtensionTests
    {
        [Fact]
        public void ExtensionsGetOwnId()
        {
            Assert.NotEqual(IActorSystemExtension<ExtensionA>.Id, IActorSystemExtension<ExtensionB>.Id);
        }
        
        [Fact]
        public void CanGetExtension()
        {
            var system = new ActorSystem();
            system.Extensions.Register(new ExtensionA
            {
                A = 123
            });
            system.Extensions.Register(new ExtensionB
            {
                B = "Hello"
            });
            
            Assert.Equal(123,system.Extensions.Get<ExtensionA>().A);
            Assert.Equal("Hello",system.Extensions.Get<ExtensionB>().B);
        }
    }
}
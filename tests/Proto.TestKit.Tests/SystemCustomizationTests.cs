using FluentAssertions;
using Xunit;

namespace Proto.TestKit.Tests
{
    public class SystemCustomizationTests : TestKitBase
    {
        public SystemCustomizationTests() : base(new ActorSystemConfig().WithDeveloperSupervisionLogging(true))
        {
            SetUp();
        }

        [Fact]
        public void CanOverrideConfigPerTest()
        {
            System.Config.DeveloperSupervisionLogging.Should().BeTrue();
        }
    }
}

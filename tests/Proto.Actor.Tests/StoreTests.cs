using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests;

public class StoreTests
{
    [Fact]
    public async Task Given_RootContextStore_SetAndGetCustomObject()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var toStore = new StoreType();
        context.Set(toStore);
        var fromStore = context.Get<StoreType>();

        Assert.Same(toStore, fromStore);
    }

    class StoreType
    {
    }
}
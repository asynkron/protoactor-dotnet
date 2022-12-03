using Proto.Analyzers.Diagnostics;

namespace Proto.Analyzers.Tests.Diagnostics;

public class TaskMessageAnalyzerTests : AnalyzerTest<TaskMessageAnalyzer>
{
    [Fact]
    public async Task ShouldNotFindAnyProblems()
    {
        var test = @"
using Proto;
using System.Threading.Tasks;

record SomeMessage(PID Target);

public class SomeActor : IActor
{
    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case SomeMessage msg:
                var response = await context.RequestAsync<string>(msg.Target, ""Hello"");
                context.Respond(response);
                break;
        }
    }
}
";

        await VerifyAnalyzerFindsNothingAsync(test);
    }

    [Fact]
    public async Task ShouldFindTaskAsMessageWhenResponding()
    {
        var test = @"
using Proto;
using System.Threading.Tasks;

record SomeMessage(PID Target);

public class SomeActor : IActor
{
    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case SomeMessage msg:
                var response = context.RequestAsync<string>(msg.Target, ""Hello"");
                context.Respond(response);
                break;
        }

        return Task.CompletedTask;
    }
}
";

        await VerifyAnalyzerAsync(test, Diagnostic().WithSpan(15, 17, 15, 42));
    }

    [Fact]
    public async Task ShouldFindTaskAsMessageWhenRequesting()
    {
        var test = @"
using Proto;
using System.Threading.Tasks;

record SomeMessage(PID Target);

public class SomeActor : IActor
{
    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case SomeMessage msg:
                var response = context.RequestAsync<string>(msg.Target, ""Hello"");
                context.Request(msg.Target,response);
                break;
        }

        return Task.CompletedTask;
    }
}
";

        await VerifyAnalyzerAsync(test, Diagnostic().WithSpan(15, 17, 15, 53));
    }

    [Fact]
    public async Task ShouldFindTaskAsMessageWhenRequestingAsync()
    {
        var test = @"
using Proto;
using System.Threading.Tasks;

record SomeMessage(PID Target);

public class SomeActor : IActor
{
    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case SomeMessage msg:
                var response = context.RequestAsync<string>(msg.Target, ""Hello"");
                await context.RequestAsync<string>(msg.Target, response);
                break;
        }
    }
}
";

        await VerifyAnalyzerAsync(test, Diagnostic().WithSpan(15, 23, 15, 73));
    }

    [Fact]
    public async Task ShouldFindTaskAsMessageWhenSending()
    {
        var test = @"
using Proto;
using System.Threading.Tasks;

record SomeMessage(PID Target);

public class SomeActor : IActor
{
    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case SomeMessage msg:
                var response = context.RequestAsync<string>(msg.Target, ""Hello"");
                context.Send(msg.Target, response);
                break;
        }

        return Task.CompletedTask;
    }
}
";

        await VerifyAnalyzerAsync(test, Diagnostic().WithSpan(15, 17, 15, 51));
    }
}
using Proto.Analyzers.Diagnostics;

namespace Proto.Analyzers.Tests.Diagnostics;

public class PoisonSelfAnalyzerTests : AnalyzerTest<PoisonSelfAnalyzer>
{
    [Fact]
    public async Task ShouldFindNoIssues()
    {
        var test = @"
using Proto;
using System.Threading.Tasks;

public class SomeActor: IActor
{
    public Task ReceiveAsync(IContext context)
    {
        return Task.CompletedTask;
    }
}
";
        await VerifyAnalyzerFindsNothingAsync(test);
    }

    [Fact]
    public async Task ShouldFindNoIssuesWhenTargetIsNotContextSelf()
    {
        var test = @"
using Proto;
using System.Threading.Tasks;

record SomeMessage(PID Self);

public class SomeActor : IActor
{
    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case SomeMessage msg:
                await context.PoisonAsync(msg.Self);
                break;
        }
    }
}
";
        await VerifyAnalyzerFindsNothingAsync(test);
    }

    [Fact]
    public async Task ShouldFindDeadlockOnPoisonAsyncSelf()
    {
        var test = @"
using Proto;
using System.Threading.Tasks;

record SomeMessage(PID Pid);

public class SomeActor : IActor
{
    public async Task ReceiveAsync(IContext context)
    {
        await context.PoisonAsync(context.Self);
    }
}
";
        await VerifyAnalyzerAsync(
            test,
            Diagnostic(DiagnosticDescriptors.Deadlock)
                .WithSpan(11, 15, 11, 48).WithArguments("PoisonAsync"));
    }
    
    [Fact]
    public async Task ShouldFindDeadlockOnStopAsyncSelf()
    {
        var test = @"
using Proto;
using System.Threading.Tasks;

record SomeMessage(PID Pid);

public class SomeActor : IActor
{
    public async Task ReceiveAsync(IContext context)
    {
        await context.StopAsync(context.Self);
    }
}
";
        await VerifyAnalyzerAsync(
            test,
            Diagnostic(DiagnosticDescriptors.Deadlock)
                .WithSpan(11, 15, 11, 46).WithArguments("StopAsync"));
    }
}
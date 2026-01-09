---
description: Generate a test bomb class to systematically debug a bug
allowed-tools: Write, Edit, Bash(dotnet test:*)
argument-hint: <BugName> <hypothesis1> [hypothesis2] [hypothesis3]...
---

Generate a test bomb class to systematically eliminate suspected causes for a bug.

For full methodology, see [agents/how-to-test-bombs.md](../../agents/how-to-test-bombs.md).

## What is a Test Bomb?

A set of targeted tests where each test targets ONE hypothesis about what's causing a bug. The pass/fail pattern reveals the failing area.

## Instructions

Given $ARGUMENTS, create a test class in `tests/Asynkron.JsEngine.Tests/` with:

1. One `[Fact]` test per hypothesis
2. Each test named `H1_FirstHypothesis`, `H2_SecondHypothesis`, etc.
3. Doc comments explaining each hypothesis
4. 10-second timeout on each test
5. `ITestOutputHelper` for logging results

## Template

```csharp
/// TEST BOMB: Systematic elimination of suspected causes for [BUG].
public class [BugName]TestBomb
{
    private readonly ITestOutputHelper _output;
    public [BugName]TestBomb(ITestOutputHelper output) => _output = output;

    /// H1: [description of first hypothesis]
    [Fact(Timeout = 10000)]
    public async Task H1_FirstHypothesis()
    {
        await using var engine = new JsEngine();
        var result = await engine.Evaluate("/* test code */");
        _output.WriteLine($"H1 Result: {result}");
        Assert.Equal("expected", result?.ToString());
    }

    /// H2: [description of second hypothesis]
    [Fact(Timeout = 10000)]
    public async Task H2_SecondHypothesis()
    {
        // ...
    }
}
```

## Example

```
/test-bomb ClosureBug "closure not capturing outer var" "let vs var difference" "block scope issue"
```

Creates `ClosureBugTestBomb.cs` with H1, H2, H3 tests.

## After Creation

Run the tests:
```bash
dotnet test tests/Asynkron.JsEngine.Tests --filter "FullyQualifiedName~TestBomb"
```

The pattern of passes/fails reveals which hypothesis is correct.

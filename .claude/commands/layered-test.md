---
description: Generate layered tests to verify each pipeline stage separately
allowed-tools: Write, Edit, Bash(dotnet test:*), Read, Grep
argument-hint: <TestName> <javascript-code>
---

Generate layered tests that verify each pipeline stage before full end-to-end runs.

For full methodology, see [agents/how-to-layered-tests.md](../../agents/how-to-layered-tests.md).

## Pipeline Layers

```
Source -> Lexer -> Parser -> AST -> Analyzers -> Evaluator -> Result
   L0       L1       L2      L3       L4           L5
```

## When to Use

- Bug manifests at runtime but stage is unclear
- Need to inspect AST/metadata before evaluation
- Validate scope/slot metadata, CPS, loop normalization
- Debug closure capture, environment chains, or slot lookups

## Instructions

Given $ARGUMENTS (test name and JS code), create a test class with tests for each layer:

1. **L2 Parser** - Assert AST structure is correct
2. **L3 Analyzers** - Assert metadata (SlotMap, ScopeId, PerIterationBindings)
3. **L4 Plans** - Assert plan contents (e.g., LoopPlan, ExecutionPlan)
4. **L5 Runtime** - Enable debug logging and assert internal operations
5. **L6 Result** - Full evaluation assertion

## Template

```csharp
public class [TestName]LayeredTests
{
    private readonly ITestOutputHelper _output;
    public [TestName]LayeredTests(ITestOutputHelper output) => _output = output;

    private const string Source = @"/* javascript code */";

    [Fact]
    public void L2_Parser_AstStructure()
    {
        var pipeline = AstTestHelpers.ParseAndAnalyze(Source);
        var node = AstTestHelpers.FindFirst<ForStatement>(pipeline.Analyzed);
        Assert.NotNull(node);
        _output.WriteLine($"Found: {node.GetType().Name}");
    }

    [Fact]
    public void L3_Analyzers_SlotMetadata()
    {
        var pipeline = AstTestHelpers.ParseAndAnalyze(Source);
        var func = AstTestHelpers.FindFirst<FunctionExpression>(pipeline.Analyzed);
        Assert.NotNull(func?.SlotMap);
        Assert.True(func.SlotMap.ContainsKey(Symbol.Create("i")));
    }

    [Fact]
    public void L4_Plans_LoopPlan()
    {
        var pipeline = AstTestHelpers.ParseAndAnalyze(Source);
        var node = AstTestHelpers.FindFirst<ForStatement>(pipeline.Analyzed);
        var plan = ((IAstCacheable<LoopPlan>)node).GetOrCreateCache();
        var bindingNames = plan.PerIterationBindings.Select(b => b.Name).ToArray();
        Assert.Contains("i", bindingNames);
    }

    [Fact(Timeout = 10000)]
    public async Task L5_Result_Evaluation()
    {
        await using var engine = new JsEngine();
        var result = await engine.Evaluate(Source);
        Assert.Equal("expected", result?.ToString());
    }
}
```

## Example

```
/layered-test ForLoopClosure "for (let i = 0; i < 3; i++) { funcs.push(() => i); }"
```

Creates `ForLoopClosureLayeredTests.cs` with tests for each pipeline stage.

## Layered vs Test Bombs

- **Layered tests** isolate failing pipeline stages
- **Test bombs** isolate failing hypotheses
- Use together: test bomb to find component, layered tests to pinpoint the stage

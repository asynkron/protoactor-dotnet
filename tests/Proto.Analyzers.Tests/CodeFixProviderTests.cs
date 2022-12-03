using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Proto.Analyzers.Tests;

public abstract class CodeFixProviderTests<TAnalyzer, TCodeFix> : AnalyzerTest<TAnalyzer>
    where TCodeFix : CodeFixProvider, new()
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    protected Task VerifyCodeFixAsync(string source, string expectedResult, DiagnosticResult diagnosticResult)
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, XUnitVerifier>
        {
            TestCode = source,
            TestState =
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60
            },
            ExpectedDiagnostics =
            {
                diagnosticResult
            },
            FixedCode = expectedResult,
        };
        foreach (var assembly in AssembliesUnderTest.Assemblies)
        {
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(assembly.Location));
        }
        
        return test.RunAsync(CancellationToken.None);
    }
}

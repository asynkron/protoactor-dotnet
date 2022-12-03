using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Proto.Analyzers.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PoisonSelfAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(DiagnosticDescriptors.PoisonAsyncDeadlock);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!invocation.MethodCalledIs("PoisonAsync")) return;
        var firstArgument = invocation.ArgumentList.Arguments.FirstOrDefault();
        if (firstArgument?.ArgumentPropertyIs("Self") != true) return;
        if (context.SemanticModel.GetSymbolInfo(firstArgument.Expression).NameMatches("Proto.IInfoContext.Self"))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.PoisonAsyncDeadlock, invocation.GetLocation()));
        }
    }
}
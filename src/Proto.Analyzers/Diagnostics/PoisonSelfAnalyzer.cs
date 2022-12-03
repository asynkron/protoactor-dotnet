using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Proto.Analyzers.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PoisonSelfAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(DiagnosticDescriptors.Deadlock);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var method = invocation.GetMethodCalled();
        if (method is null) return;
        if (!method.Equals("PoisonAsync", StringComparison.Ordinal) && !method.Equals("StopAsync", StringComparison.Ordinal)) return;
        var firstArgument = invocation.ArgumentList.Arguments.FirstOrDefault();
        if (firstArgument?.ArgumentPropertyIs("Self") != true) return;
        if (context.SemanticModel.GetSymbolInfo(firstArgument.Expression).NameMatches("Proto.IInfoContext.Self"))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.Deadlock, invocation.GetLocation(), method));
        }
    }
}
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Proto.Analyzers.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TaskMessageAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(DiagnosticDescriptors.TaskAsMessage);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count == 0) return;
        var method = invocation.GetMethodCalled();
        if (method is null) return;
        switch (method)
        {
            case "Send":
            case "Request":
            case "RequestAsync":
                Check(context, invocation, 1);
                break;
            case "Respond":
                Check(context, invocation, 0);
                break;
        }
    }

    private static void Check(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, int messageIndex)
    {
        if (invocation.ArgumentList.Arguments.Count <= messageIndex) return;
        if (!IsTaskType(context, invocation.ArgumentList.Arguments[messageIndex].Expression)) return;

        var invocationSymbolInfo = context.SemanticModel.GetSymbolInfo(invocation.Expression);

        if (invocationSymbolInfo.Symbol?.ToDisplayString().StartsWith("Proto.", StringComparison.Ordinal) == true)
        {
            ReportDiagnostic(context, invocation);
        }
    }

    private static bool IsTaskType(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        var type = context.SemanticModel.GetTypeInfo(expression).Type;
        if (type is null) return false;
        return type.Name == "Task";
    }

    private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.TaskAsMessage, invocation.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }
}
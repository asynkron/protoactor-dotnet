using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Proto.Analyzers;

internal static class SyntaxExtensions
{
    public static bool ArgumentPropertyIs(this ArgumentSyntax argument, string name)
    {
        if (argument.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        return memberAccess.Name.Identifier.ValueText == name;
    }

    public static string? GetMethodCalled(this InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return default;
        return memberAccess.Name.Identifier.ValueText;
    }
}
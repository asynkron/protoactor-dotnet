using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Proto.Analyzers;

internal static class SyntaxExtensions
{
    public static bool ArgumentPropertyIs(this ArgumentSyntax argument, string name)
    {
        if (argument.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        return memberAccess.Name.Identifier.ValueText == name;
    }

    public static bool MethodCalledIs(this InvocationExpressionSyntax invocation, string methodName)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        return memberAccess.Name.Identifier.ValueText == methodName;
    }
}
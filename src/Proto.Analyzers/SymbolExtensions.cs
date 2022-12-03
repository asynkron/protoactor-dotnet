using Microsoft.CodeAnalysis;

namespace Proto.Analyzers;

internal static class SymbolExtensions
{
    public static bool NameMatches(this SymbolInfo symbolInfo, string symbolName) => symbolInfo.Symbol.NameMatches(symbolName);
    public static bool NameMatches(this ISymbol? symbol, string symbolName)
    {
        return symbol?.ToDisplayString() == symbolName;
    }
}
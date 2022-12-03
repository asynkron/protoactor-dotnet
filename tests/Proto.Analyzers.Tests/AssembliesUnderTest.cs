using System.Reflection;

namespace Proto.Analyzers.Tests;

internal static class AssembliesUnderTest
{
    public static readonly Assembly[] Assemblies =
    {
        typeof(IActor).Assembly,
    };
}
using System.Reflection;
using Google.Protobuf;

namespace Proto.Analyzers.Tests;

internal static class AssembliesUnderTest
{
    public static readonly Assembly[] Assemblies =
    {
        typeof(IActor).Assembly,
        typeof(IBufferMessage).Assembly,
    };
}
using Xunit.Sdk;

namespace Proto.Remote.Tests;

public sealed class DisplayTestMethodNameAttribute : BeforeAfterTestAttribute
{
    // public override void Before(MethodInfo methodUnderTest)
    // {
    //     Console.WriteLine();
    //     Console.WriteLine($"******** Running '{methodUnderTest.Name}.' ********");
    // }

    // public override void After(MethodInfo methodUnderTest)
    // {
    //     Console.WriteLine($"******** Finished '{methodUnderTest.Name}.' ********");
    // }
}
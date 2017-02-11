var target = Argument("target", "UnitTest");

Task("Restore")
    .Does(() => {
        DotNetCoreRestore();
    });
Task("Build")
    .IsDependentOn("Restore")
    .Does(() => {
        DotNetCoreBuild("ProtoActor.sln", new DotNetCoreBuildSettings {
            Configuration = "Release"
        });
    });

Task("UnitTest")
    .IsDependentOn("Build")
    .Does(() => {
        DotNetCoreTest("tests/Proto.Actor.Tests//Proto.Actor.Tests.csproj");
    });

RunTarget(target);
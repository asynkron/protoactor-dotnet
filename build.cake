var target = Argument("target", "Default");

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

Task("Pack")
    .IsDependentOn("Build")
    .Does(() => {
        foreach(var proj in GetFiles("src/**/*.csproj")) {
            DotNetCorePack(proj.ToString(), new DotNetCorePackSettings {
                OutputDirectory = "out"
            });
        }
    });

Task("Default")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("UnitTest")
    .IsDependentOn("Pack");

RunTarget(target);
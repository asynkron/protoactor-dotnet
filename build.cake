#addin Cake.Git

var target = Argument("target", "Default");
var mygetApiKey = Argument<string>("mygetApiKey", null);

var currentBranch = GitBranchCurrent("./").FriendlyName;
var versionSuffix = currentBranch == "master" ? null : currentBranch;
var version = Argument<string>("version", "0.1.0");
if (versionSuffix != null)
    version += "-" + versionSuffix;

Task("Restore")
    .Does(() => {
        DotNetCoreRestore();
    });
Task("Build")
    .IsDependentOn("Restore")
    .Does(() => {
        DotNetCoreBuild("ProtoActor.sln", new DotNetCoreBuildSettings {
            Configuration = "Release",
            ArgumentCustomization = args => args.Append("/property:Version=" + version)
        });
    });

Task("UnitTest")
    .Does(() => {
        DotNetCoreTest("tests/Proto.Actor.Tests//Proto.Actor.Tests.csproj");
    });

Task("Pack")
    .Does(() => {
        foreach(var proj in GetFiles("src/**/*.csproj")) {
            DotNetCorePack(proj.ToString(), new DotNetCorePackSettings {
                OutputDirectory = "out",
                Configuration = "Release",
                VersionSuffix = versionSuffix,
                ArgumentCustomization = args => args.Append("/property:Version=" + version)
            });
        }
    });
Task("Push")
    .Does(() => {
        NuGetPush("out/*.nupkg", new NuGetPushSettings {
            Source = "https://www.myget.org/F/protoactor/api/v2/package",
            ApiKey = mygetApiKey
        });
    });

Task("Default")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("UnitTest")
    .IsDependentOn("Pack")
    //.IsDependentOn("Push")
    ;

RunTarget(target);
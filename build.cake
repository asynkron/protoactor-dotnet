#addin Cake.Git

var target = Argument("target", "Default");
var mygetApiKey = Argument<string>("mygetApiKey", null);
var packageVersion = Argument<string>("packageVersion", "0.1.0");
var currentBranch = Argument<string>("currentBranch", GitBranchCurrent("./").FriendlyName);

var versionSuffix = currentBranch == "master" ? null : currentBranch;
if (versionSuffix != null)
    packageVersion += "-" + versionSuffix;

Information("Version: " + packageVersion);

Task("Restore")
    .Does(() => {
        DotNetCoreRestore();
    });
Task("Build")
    .IsDependentOn("Restore")
    .Does(() => {
        DotNetCoreBuild("ProtoActor.sln", new DotNetCoreBuildSettings {
            Configuration = "Release",
            ArgumentCustomization = args => args.Append("/property:Version=" + packageVersion)
        });
    });

Task("UnitTest")
    .Does(() => {
        DotNetCoreTest("tests/Proto.Actor.Tests//Proto.Actor.Tests.csproj");
        DotNetCoreTest("tests/Proto.MailBox.Tests//Proto.MailBox.Tests.csproj");
        DotNetCoreTest("tests/Proto.Remote.Tests//Proto.Remote.Tests.csproj");
    });

Task("Pack")
    .Does(() => {
        foreach(var proj in GetFiles("src/**/*.csproj")) {
            DotNetCorePack(proj.ToString(), new DotNetCorePackSettings {
                OutputDirectory = "out",
                Configuration = "Release",
                VersionSuffix = versionSuffix,
                ArgumentCustomization = args => args.Append("/property:Version=" + packageVersion)
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
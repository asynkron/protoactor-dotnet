#addin Cake.Git

var packageVersion = "0.1.6";

var target = Argument("target", "Default");
var mygetApiKey = Argument<string>("mygetApiKey", null);
var currentBranch = Argument<string>("currentBranch", GitBranchCurrent("./").FriendlyName);
var buildNumber = Argument<string>("buildNumber", null);

var versionSuffix = "";
if (currentBranch != "master") {
    versionSuffix += "-" + currentBranch;
    if (buildNumber != null) {
        versionSuffix += "-build" + buildNumber.PadLeft(5, '0');
    }
    packageVersion += versionSuffix;
}

Information("Version: " + packageVersion);

Task("PatchVersion")
    .Does(() => {
        foreach(var proj in GetFiles("src/**/*.csproj")) {
            Information("Patching " + proj);
            XmlPoke(proj, "/Project/PropertyGroup/Version", packageVersion);
        }
    });
Task("Restore")
    .IsDependentOn("PatchVersion")
    .Does(() => {
        DotNetCoreRestore();
    });
Task("Build")
    .IsDependentOn("Restore")
    .Does(() => {
        DotNetCoreBuild("ProtoActor.sln", new DotNetCoreBuildSettings {
            Configuration = "Release",
        });
    });
Task("UnitTest")
    .Does(() => {
        foreach(var proj in GetFiles("tests/**/*.Tests.csproj")) {
            DotNetCoreTest(proj.ToString(), new DotNetCoreTestSettings {
                NoBuild = true,
            });
        }
    });
Task("Pack")
    .Does(() => {
        foreach(var proj in GetFiles("src/**/*.csproj")) {
            DotNetCorePack(proj.ToString(), new DotNetCorePackSettings {
                OutputDirectory = "out",
                Configuration = "Release",
                NoBuild = true,
            });
        }
    });
Task("Push")
    .Does(() => {
        var pkgs = GetFiles("out/*.nupkg");
        foreach(var pkg in pkgs) {
            NuGetPush(pkg, new NuGetPushSettings {
                Source = "https://www.myget.org/F/protoactor/api/v2/package",
                ApiKey = mygetApiKey
            });
        }
    });

Task("Default")
    .IsDependentOn("Restore")
    .IsDependentOn("PatchVersion")
    .IsDependentOn("Build")
    .IsDependentOn("UnitTest")
    .IsDependentOn("Pack")
    ;

RunTarget(target);

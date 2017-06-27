#addin Cake.Git

var packageVersion = "0.1.10";

var target = Argument("target", "Default");
var mygetApiKey = Argument<string>("mygetApiKey", null);
var currentBranch = Argument<string>("currentBranch", GitBranchCurrent("./").FriendlyName);
var buildNumber = Argument<string>("buildNumber", null);
var configuration = "Release";

var versionSuffix = "";
if (currentBranch != "master") {
    versionSuffix += "-" + currentBranch;
    if (buildNumber != null) {
        versionSuffix += "-build" + buildNumber.PadLeft(5, '0');
    }
    packageVersion += versionSuffix;
}

bool IsPRBuild()
{
    return EnvironmentVariable("APPVEYOR_PULL_REQUEST_TITLE") != null;
}

Information("Version: " + packageVersion);

Task("PatchVersion")
    .Does(() => 
    {
        if (IsPRBuild())
        {
            return;
        }
        foreach(var proj in GetFiles("src/**/*.csproj")) 
        {
            Information("Patching " + proj);
            XmlPoke(proj, "/Project/PropertyGroup/Version", packageVersion);

            if (IsRunningOnWindows())
            {
                Information("Installing Strong Naming " + proj);
                StartProcess("dotnet", new ProcessSettings{ Arguments = $"add {proj} package StrongNamer" } );
            }
        }
    });

Task("Restore")
    .Does(() => 
    {
        DotNetCoreRestore();
    });

Task("Build")
    .Does(() => 
    {
        DotNetCoreBuild("ProtoActor.sln", new DotNetCoreBuildSettings 
        {
            Configuration = configuration,
        });
    });

Task("UnitTest")
    .Does(() => 
    {
        foreach(var proj in GetFiles("tests/**/*.Tests.csproj")) 
        {
            DotNetCoreTest(proj.ToString(), new DotNetCoreTestSettings 
            {
                NoBuild = true,
                Configuration = configuration
            });
        }
    });

Task("Pack")
    .Does(() => 
    {
        if (IsPRBuild())
        {
            return;
        }
        foreach(var proj in GetFiles("src/**/*.csproj")) 
        {
            DotNetCorePack(proj.ToString(), new DotNetCorePackSettings 
            {
                OutputDirectory = "out",
                Configuration = configuration,
                NoBuild = true,
            });
        }
    });

Task("Push")
    .Does(() => 
    {
        if (IsPRBuild())
        {
            return;
        }
        var pkgs = GetFiles("out/*.nupkg");
        foreach(var pkg in pkgs) 
        {
            NuGetPush(pkg, new NuGetPushSettings 
            {
                Source = "https://www.myget.org/F/protoactor/api/v2/package",
                ApiKey = mygetApiKey
            });
        }
    });

Task("Default")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("UnitTest");

RunTarget(target);

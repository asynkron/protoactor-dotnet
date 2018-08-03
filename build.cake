#addin Cake.Git

var packageVersion = "0.2.0";

var target = Argument("target", "Default");
var mygetApiKey = Argument<string>("mygetApiKey", null);
var nugetApiKey = Argument<string>("nugetApiKey", null);
var currentBranch = Argument<string>("currentBranch", GitBranchCurrent("./").FriendlyName);
var buildNumber = Argument<string>("buildNumber", null);
var pullRequestTitle = Argument<string>("pullRequestTitle", null);
var configuration = "Release";
var isPullRequestBuild = !string.IsNullOrWhiteSpace(pullRequestTitle);

Task("PrintEnvironment")
    .Does(() =>
    {
        foreach(var envVar in EnvironmentVariables())
        {
            Information("{0}: \"{1}\"", envVar.Key, envVar.Value);
        }
    });
Task("PatchVersion")
    .Does(() => 
    {
        if (isPullRequestBuild)
        {
            Information("Pull request build. Skipping version patching.");
            return;
        }
        var versionSuffix = "";
        if (currentBranch != "master") {
            if (buildNumber != null) {
                versionSuffix += "-build" + buildNumber.PadLeft(5, '0');
            }
            versionSuffix += "-" + currentBranch;
            if (versionSuffix.Length > 20) {
                versionSuffix = versionSuffix.Substring(0, 20);
            }
            packageVersion += versionSuffix;
        }
        Information("Version: " + packageVersion);        

        foreach(var proj in GetFiles("src/**/*.csproj")) 
        {
            Information("Patching " + proj);
            XmlPoke(proj, "/Project/PropertyGroup/Version", packageVersion);
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
            if (IsRunningOnWindows()) 
            {
                DotNetCoreTest(proj.ToString(), new DotNetCoreTestSettings 
                {
                    NoBuild = true,
                    Configuration = configuration
                });
            } 
            else // dotnet test cannot run full framework tests on mac so ignore them
            {
                DotNetCoreTest(proj.ToString(), new DotNetCoreTestSettings 
                {
                    NoBuild = true,
                    Framework = "netcoreapp2.0",
                    Configuration = configuration
                });
            }
        }
    });

Task("Pack")
    .Does(() => 
    {
        if (isPullRequestBuild)
        {
            Information("Pull request build. Skipping NuGet packing.");
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
        if (isPullRequestBuild)
        {
            Information("Pull request build. Skipping NuGet push.");
            return;
        }
        var nuGetPushSettings = new NuGetPushSettings 
            {
                Source = "https://www.nuget.org/api/v2/package",
                ApiKey = nugetApiKey
            };
        var myGetPushSettings = new NuGetPushSettings 
            {
                Source = "https://www.myget.org/F/protoactor/api/v2/package",
                ApiKey = mygetApiKey
            };

        var pkgs = GetFiles("out/*.nupkg");
        foreach(var pkg in pkgs) 
        {
            NuGetPush(pkg, myGetPushSettings);
        }
        if (currentBranch == "master")
        {
            Information("Master build. Publishing to NuGet.");
            foreach(var pkg in pkgs) 
            {
                NuGetPush(pkg, nuGetPushSettings);
            }
        }
        else
        {
            Information("Non-master build. Not publishing to NuGet. Current branch: " + currentBranch);
        }
    });

Task("Default")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("UnitTest");

RunTarget(target);

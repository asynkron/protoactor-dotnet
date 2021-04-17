using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Proto.GrainGenerator;


namespace MSBuildTasks
{
    public class ProtoGen : Task
    {

        public override bool Execute()
        {
            // This will get the current WORKING directory (i.e. \bin\Debug)
            var path1 = Environment.CurrentDirectory;
            // or: Directory.GetCurrentDirectory() gives the same result
            
            // This will get the current PROJECT bin directory (ie ../bin/)
            var path2 = Directory.GetParent(path1).Parent.FullName;
            
            // This will get the current PROJECT directory
            var path3 = Directory.GetParent(path1!)!.Parent!.Parent!.FullName;
            
            Log.LogMessage(MessageImportance.High, "Path1 " + path1);
            Log.LogMessage(MessageImportance.High, "Path2 " + path2);
            Log.LogMessage(MessageImportance.High, "Path3 " + path3);
            
            
            Log.LogMessage(MessageImportance.High, "Running Proto.GrainGenerator");

            var projectFile = BuildEngine.ProjectFileOfTaskNode;
            Log.LogMessage(MessageImportance.High, $"Processing Project file: {projectFile}");
            var projectDirectory = Path.GetDirectoryName(projectFile)!;
            var protoFiles = Directory.GetFiles(projectDirectory, "*.proto", new EnumerationOptions
                {
                    RecurseSubdirectories = true
                }
            )!;
            
            foreach (var protoFile in protoFiles)
            {
                Log.LogMessage(MessageImportance.High, $"Processing Proto file: {protoFile}");
                var outputFile = Path.Combine(projectDirectory!, $"{protoFile}.cs");
            
                var fiIn = new FileInfo(protoFile);
                var fiOut = new FileInfo(outputFile);
                Generator.Generate(fiIn, fiOut, Array.Empty<DirectoryInfo>(), Log, projectDirectory);
            }

            return true;
        }
    }
}
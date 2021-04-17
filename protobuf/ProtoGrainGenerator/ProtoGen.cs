using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Proto.GrainGenerator;


namespace MSBuildTasks
{
    public class ProtoGen : Task
    {

        
        // ReSharper disable once MemberCanBePrivate.Global
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        [Required]
        public string BaseIntermediateOutputPath { get; set; }
        
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High,"Intermediate OutputPath: " + BaseIntermediateOutputPath);

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
                Generator.Generate(fiIn, fiOut, Array.Empty<DirectoryInfo>(), Log, BaseIntermediateOutputPath);
            }

            return true;
        }
    }
}
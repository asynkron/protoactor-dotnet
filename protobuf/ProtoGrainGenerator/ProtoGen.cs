using System;
using System.IO;
using System.Linq;
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

        [Required]
        public string MSBuildProjectFullPath { get; set; }

        public string AdditionalImportDirs { get; set; }
        
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, $"Intermediate OutputPath: {BaseIntermediateOutputPath}");
            Log.LogMessage(MessageImportance.High, $"Additional import directories: {AdditionalImportDirs}");
            Log.LogMessage(MessageImportance.High, "Running Proto.GrainGenerator");

            var projectFile = MSBuildProjectFullPath;
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
                var rel = Path.GetRelativePath(projectDirectory, protoFile);

                var potatoDirectory = Path.Combine(BaseIntermediateOutputPath!, "protopotato");
                Directory.CreateDirectory(potatoDirectory);
                
                var outputFile = Path.Combine(potatoDirectory, $"{rel}.cs");
                Log.LogMessage(MessageImportance.High, $"Output file path: {outputFile}");

                var inputFileInfo = new FileInfo(protoFile);
                var outputFileInfo = new FileInfo(outputFile);

                var importPaths = 
                    AdditionalImportDirs
                        .Split(";", StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => new DirectoryInfo(p)).ToArray();
                
                Generator.Generate(inputFileInfo, outputFileInfo, importPaths, Log, projectDirectory);
            }

            return true;
        }
    }
}
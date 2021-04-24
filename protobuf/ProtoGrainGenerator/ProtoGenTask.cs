using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Proto.GrainGenerator;


namespace MSBuildTasks
{
    public class ProtoGenTask : Task
    {
        // ReSharper disable once MemberCanBePrivate.Global
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        [Required]
        public string IntermediateOutputPath { get; set; }

        [Required]
        public string MSBuildProjectFullPath { get; set; }

        public string AdditionalImportDirs { get; set; }

        public string TemplatePath { get; set; }

        public ITaskItem[] ProtoFile { get; set; }
        public ITaskItem[] ProtoTemplate { get; set; }
        
        public override bool Execute()
        {
            AdditionalImportDirs ??= "";

            if (ProtoFile != null)
            {
                foreach (var item in ProtoFile)
                {
                    Log.LogMessage(MessageImportance.High, "ProtoFile Item Spec:"+ item.ItemSpec);
                }
            }
            else
            {
                Log.LogMessage(MessageImportance.High, "No items in ProtoFile property....");
            }
            
            if (ProtoTemplate != null)
            {
                foreach (var item in ProtoTemplate)
                {
                    Log.LogMessage(MessageImportance.High, "ProtoTemplate Item Spec:"+ item.ItemSpec);
                }
            }
            else
            {
                Log.LogMessage(MessageImportance.High, "No items in ProtoTemplate property....");
            }
            
            Log.LogMessage(MessageImportance.High, $"Intermediate OutputPath: {IntermediateOutputPath}");
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

                var potatoDirectory = Path.Combine(IntermediateOutputPath!, "protopotato");
                Directory.CreateDirectory(potatoDirectory);
                
                var outputFile = Path.Combine(potatoDirectory, $"{rel}.cs");
                Log.LogMessage(MessageImportance.High, $"Output file path: {outputFile}");

                var inputFileInfo = new FileInfo(protoFile);
                var outputFileInfo = new FileInfo(outputFile);

                var importPaths = 
                    AdditionalImportDirs
                        .Split(";", StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Select(p => Path.GetRelativePath(projectDirectory, p))
                        .Select(p => new DirectoryInfo(p)).ToArray();

                foreach (var importPath in importPaths)
                {
                    Log.LogMessage(MessageImportance.High, $"Import path {importPath.FullName}");
                }
                
                Generator.Generate(inputFileInfo, outputFileInfo, importPaths, Log, projectDirectory, TemplatePath);
            }

            return true;
        }
    }
}
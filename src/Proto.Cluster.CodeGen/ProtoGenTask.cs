using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Proto.Cluster.CodeGen;

public class ProtoGenTask : Task
{
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    [Required] public string IntermediateOutputPath { get; set; } = null!;

    [Required] public string MSBuildProjectFullPath { get; set; } = null!;

    public ITaskItem[] ProtoFile { get; set; } = Array.Empty<ITaskItem>();

    public override bool Execute()
    {
        var projectFile = MSBuildProjectFullPath;
        Log.LogMessage(MessageImportance.High, $"Processing Project file: {projectFile}");
        Log.LogMessage(MessageImportance.High, $"Intermediate OutputPath: {IntermediateOutputPath}");
        var projectDirectory = Path.GetDirectoryName(projectFile)!;

        var potatoDirectory = Path.Combine(IntermediateOutputPath, "protopotato");
        EnsureDirExistsAndIsEmpty(potatoDirectory);

        if (ProtoFile.Any())
        {
            foreach (var item in ProtoFile)
            {
                var templateFiles = item.GetMetadata("TemplateFiles");
                var additionalImportDirs = item.GetMetadata("AdditionalImportDirs");
                var protoFile = item.ItemSpec;

                Log.LogMessage(MessageImportance.High,
                    $"ProtoFile Item File:{item.ItemSpec}, Imports:{additionalImportDirs}, Templates:{templateFiles}"
                );

                ProcessFile(projectDirectory, potatoDirectory, protoFile, additionalImportDirs, templateFiles);
            }
        }
        else
        {
            Log.LogMessage(MessageImportance.High, "No files marked as 'ProtoFile' in project....");
        }

        Log.LogMessage(MessageImportance.High, "ProtoGen completed successfully");

        return true;
    }

    private static void EnsureDirExistsAndIsEmpty(string? potatoDirectory)
    {
        Directory.CreateDirectory(potatoDirectory);
        var di = new DirectoryInfo(potatoDirectory);

        foreach (var file in di.GetFiles())
        {
            file.Delete();
        }
    }

    private void ProcessFile(string projectDirectory, string objDirectory, string protoFile,
        string additionalImportDirsString, string templateFilesString)
    {
        Log.LogMessage(MessageImportance.High, $"Processing Proto file: {protoFile}");
        var inputFileInfo = new FileInfo(protoFile);
        var importPaths = GetImportPaths(projectDirectory, additionalImportDirsString);
        var templateFiles = GetTemplatePaths(projectDirectory, templateFilesString);

        if (!templateFiles.Any())
        {
            var template = Template.DefaultTemplate;
            var outputFileName = OutputFileName.GetOutputFileName(inputFileInfo);

            GenerateFile(projectDirectory, objDirectory, inputFileInfo, importPaths, template, outputFileName);
        }
        else
        {
            foreach (var templateFile in templateFiles)
            {
                var template = File.ReadAllText(templateFile.FullName, Encoding.Default);
                var outputFileName = OutputFileName.GetOutputFileName(inputFileInfo, templateFile);

                GenerateFile(projectDirectory, objDirectory, inputFileInfo, importPaths, template, outputFileName);
            }
        }
    }

    private void GenerateFile(
        string projectDirectory,
        string objDirectory,
        FileInfo inputFileInfo,
        DirectoryInfo[] importPaths,
        string template,
        string outputFileName
    )
    {
        var outputFilePath = Path.Combine(objDirectory, outputFileName);
        Log.LogMessage(MessageImportance.High, $"Output file path: {outputFilePath}");
        var outputFileInfo = new FileInfo(outputFilePath);
        Generator.Generate(inputFileInfo, outputFileInfo, importPaths, Log, projectDirectory, template);
    }

    private DirectoryInfo[] GetImportPaths(string projectDirectory, string additionalImportDirsString)
    {
        var importPaths =
            additionalImportDirsString
                .Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Select(p => PathPolyfill.GetRelativePath(projectDirectory, p))
                .Select(p => new DirectoryInfo(p))
                .ToArray();

        foreach (var importPath in importPaths)
        {
            Log.LogMessage(MessageImportance.High, $"Import path {importPath.FullName}");
        }

        return importPaths;
    }

    private FileInfo[] GetTemplatePaths(string projectDirectory, string templateFilesString)
    {
        var templateFilesArr =
            templateFilesString
                .Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Select(p => PathPolyfill.GetRelativePath(projectDirectory, p))
                .Select(p => new FileInfo(p))
                .ToArray();

        foreach (var templatePath in templateFilesArr)
        {
            Log.LogMessage(MessageImportance.High, $"Template path {templatePath.FullName}");
        }

        return templateFilesArr;
    }
}
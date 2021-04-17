using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
// using Proto.GrainGenerator;
using ProtoGrainGenerator;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace MSBuildTasks
{
    public class ProtoGen : MSBuildTask
    {
        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) => EmbeddedAssembly.Get(args.Name);

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "Running ProtoGen!!!!");
            // EmbeddedAssembly.Load("ProtoGrainGenerator.deps.Handlebars.dll","Handlebars.dll");
            // EmbeddedAssembly.Load("ProtoGrainGenerator.deps.protobuf-net.dll","protobuf-net.dll");
            // EmbeddedAssembly.Load("ProtoGrainGenerator.deps.protobuf-net.Core.dll","protobuf-net.Core.dll");
            // EmbeddedAssembly.Load("ProtoGrainGenerator.deps.protobuf-net.Reflection.dll","protobuf-net.Reflection.dll");
            //
            // AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            //
            // var currentProject = BuildEngine.ProjectFileOfTaskNode;
            // var dir = Path.GetDirectoryName(currentProject)!;
            // var protoFiles = Directory.GetFiles(dir, "*.proto", new EnumerationOptions
            //     {
            //         RecurseSubdirectories = true
            //     }
            // )!;
            //
            // foreach (var protoFile in protoFiles)
            // {
            //     Log.LogMessage(MessageImportance.High, $"Protofile! {protoFile}");
            //     var protoDir = Path.GetDirectoryName(protoFile);
            //     var outputFile = Path.Combine(protoDir!, protoFile + ".cs");
            //
            //     var fiIn = new FileInfo(protoFile);
            //     var fiOut = new FileInfo(outputFile);
            //     Generator.GenerateOne(fiIn, fiOut, Array.Empty<DirectoryInfo>());
            // }

            return true;
        }
    }
}
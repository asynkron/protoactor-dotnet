// -----------------------------------------------------------------------
// <copyright file="Generator.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Proto.GrainGenerator
{
    public static class Generator
    {
        internal static void Generate(FileInfo input, FileInfo output, IEnumerable<DirectoryInfo> importPath, TaskLoggingHelper log, string rootPath)
        {
            var set = GetSet(importPath);

            var r = input.OpenText();
            var defaultOutputName = output?.FullName ?? Path.GetFileNameWithoutExtension(input.Name);
            var rel = Path.GetRelativePath(rootPath, defaultOutputName);
            
            set.Add(rel, true, r);

            set.Process();
            var gen = new CodeGenerator();
            var res = gen.Generate(set).ToList();

            foreach (var x in res)
            {
                log.LogMessage(MessageImportance.High, $"Saving generated file {x.Name}");
                File.WriteAllText(x.Name, x.Text);
            }
        }

        private static FileDescriptorSet GetSet(IEnumerable<DirectoryInfo> importPaths)
        {
            var set = new FileDescriptorSet();

            foreach (var path in importPaths)
            {
                set.AddImportPath(path.FullName);
            }

            return set;
        }
    }
}
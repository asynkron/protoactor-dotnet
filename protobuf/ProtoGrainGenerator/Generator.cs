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
using GrainGenerator;

namespace Proto.GrainGenerator
{
    public static class Generator
    {
        internal static void GenerateOne(FileInfo input, FileInfo output, IEnumerable<DirectoryInfo> importPath)
        {
            var set = GetSet(importPath);

            var r = input.OpenText();
            var defaultOutputName = output?.FullName ?? Path.GetFileNameWithoutExtension(input.Name);
            set.Add(defaultOutputName, true, r);

            ProcessAndWriteFiles(set);
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

        private static void ProcessAndWriteFiles(FileDescriptorSet set)
        {
            set.Process();
            var gen = new GrainGen();
            var res = gen.Generate(set).ToList();

            foreach (var x in res)
            {
                Console.WriteLine($"Writing generated file: {x.Name}");
                File.WriteAllText(x.Name, x.Text);
            }
        }
    }
}
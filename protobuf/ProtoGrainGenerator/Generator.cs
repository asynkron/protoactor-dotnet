// -----------------------------------------------------------------------
// <copyright file="Generator.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.Reflection;
using GrainGenerator;

namespace ProtoGrainGenerator
{
    public static class Generator
    {
        internal static Task GenerateOne(FileInfo input, FileInfo output, IEnumerable<DirectoryInfo> importPath)
        {
            var set = GetSet(importPath);

            var r = input.OpenText();
            var defaultOutputName = output?.FullName ?? Path.GetFileNameWithoutExtension(input.Name);
            set.Add(defaultOutputName, true, r);

            return ProcessAndWriteFiles(set);
        }

        internal static Task GenerateMany(IEnumerable<FileInfo> input, IEnumerable<DirectoryInfo> importPath)
        {
            var set = GetSet(importPath);

            foreach (var proto in input)
            {
                var r = proto.OpenText();
                var defaultOutputName = Path.GetFileNameWithoutExtension(proto.Name);
                set.Add(defaultOutputName, true, r);
            }

            return ProcessAndWriteFiles(set);
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

        private static Task ProcessAndWriteFiles(FileDescriptorSet set)
        {
            set.Process();

            var gen = new GrainGen();
            var res = gen.Generate(set).ToList();

            return Task.WhenAll(res.Select(x =>
                    {
                        Console.WriteLine($"Writing generated file: {x.Name}");
                        return File.WriteAllTextAsync(x.Name, x.Text);
                    }
                )
            );
        }
    }
}
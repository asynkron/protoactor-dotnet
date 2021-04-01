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
using ProtoBuf.Reflection;

namespace ProtoGrainGenerator
{
    public static class Generator
    {
        internal static Task GenerateOne(FileInfo input, FileInfo output, IEnumerable<DirectoryInfo> importPath)
        {
            FileDescriptorSet set = GetSet(importPath);

            StreamReader r = input.OpenText();
            string defaultOutputName = output?.FullName ?? Path.GetFileNameWithoutExtension(input.Name);
            set.Add(defaultOutputName, true, r);

            return ProcessAndWriteFiles(set);
        }

        internal static Task GenerateMany(IEnumerable<FileInfo> input, IEnumerable<DirectoryInfo> importPath)
        {
            FileDescriptorSet set = GetSet(importPath);

            foreach (FileInfo proto in input)
            {
                StreamReader r = proto.OpenText();
                string? defaultOutputName = Path.GetFileNameWithoutExtension(proto.Name);
                set.Add(defaultOutputName, true, r);
            }

            return ProcessAndWriteFiles(set);
        }

        private static FileDescriptorSet GetSet(IEnumerable<DirectoryInfo> importPaths)
        {
            FileDescriptorSet set = new FileDescriptorSet();

            foreach (DirectoryInfo path in importPaths)
            {
                set.AddImportPath(path.FullName);
            }

            return set;
        }

        private static Task ProcessAndWriteFiles(FileDescriptorSet set)
        {
            set.Process();

            GrainGen gen = new GrainGen();
            List<CodeFile> res = gen.Generate(set).ToList();

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

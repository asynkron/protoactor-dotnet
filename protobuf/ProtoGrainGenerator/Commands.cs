// -----------------------------------------------------------------------
// <copyright file="Commands.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;

namespace ProtoGrainGenerator
{
    public static class Commands
    {
        public static RootCommand CreateCommands()
        {
            var importPath =
                new Option<IEnumerable<DirectoryInfo>>("--importPath", () => new Empty(), "Path for additional imports")
                    .ExistingOnly();

            var rootCommand = new RootCommand
            {
                new Argument<FileInfo>("input", "Proto file name").ExistingOnly(),
                new Argument<FileInfo>("output", () => null, "Generated file name"),
                importPath
            };
            rootCommand.Description = "Generate code from a single proto file";

            rootCommand.Handler =
                CommandHandler.Create<FileInfo, FileInfo, IEnumerable<DirectoryInfo>>(Generator.GenerateOne);

            var generateCommand = new Command("many", "Generate code from proto files")
            {
                new Argument<IEnumerable<FileInfo>>("input", "Proto file names"),
                importPath
            };

            generateCommand.Handler =
                CommandHandler.Create<IEnumerable<FileInfo>, IEnumerable<DirectoryInfo>>(Generator.GenerateMany);
            rootCommand.AddCommand(generateCommand);

            return rootCommand;
        }
    }

    internal class Empty : IEnumerable<DirectoryInfo>
    {
        public IEnumerator<DirectoryInfo> GetEnumerator() => Enumerable.Empty<DirectoryInfo>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public override string ToString() => "<none>";
    }
}
// -----------------------------------------------------------------------
// <copyright file="OutputFileNameTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.IO;
using FluentAssertions;
using Xunit;

namespace Proto.Cluster.CodeGen.Tests
{
    public class OutputFileNameTests
    {
        [Fact]
        public void CanGetOutputFileName()
        {
            var fileInfo = new FileInfo(@"Some\Namespace\Actors.proto");

            var outputFileName = OutputFileName.GetOutputFileName(fileInfo);

            outputFileName.Should().MatchRegex(@"Actors-[0-9A-F]+\.cs");
        }
        
        [Fact]
        public void CanGetOutputFileNameWithTemplate()
        {
            var inputFileInfo = new FileInfo(@"Some\Namespace\Actors.proto");
            var templateFileInfo = new FileInfo(@"Some\Namespace\Template.cs");

            var outputFileName = OutputFileName.GetOutputFileName(inputFileInfo, templateFileInfo);

            outputFileName.Should().MatchRegex(@"Actors-[0-9A-F]+\.cs");
        }

        [Fact]
        public void CanGetDifferentFileNamesForDifferentPaths()
        {
            var firstFileName = OutputFileName.GetOutputFileName(new FileInfo(@"First\Namespace\Actors.proto"));
            var secondFileName = OutputFileName.GetOutputFileName(new FileInfo(@"Second\Namespace\Actors.proto"));

            firstFileName.Should().NotBe(secondFileName);
        }
        
        [Fact]
        public void CanGetDifferentFileNamesForDifferentTemplates()
        {
            var inputFile = new FileInfo(@"First\Namespace\Actors.proto");
            
            var firstFileName = OutputFileName.GetOutputFileName(inputFile, new FileInfo(@"Some\Namespace\Template-1.cs"));
            var secondFileName = OutputFileName.GetOutputFileName(inputFile, new FileInfo(@"Some\Namespace\Template-2.cs"));

            firstFileName.Should().NotBe(secondFileName);
        }
    }
}
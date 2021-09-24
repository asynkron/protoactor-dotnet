// -----------------------------------------------------------------------
// <copyright file="PathPolyfillTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Proto.Cluster.CodeGen.Tests
{
    public class PathPolyfillTests
    {
        [Theory]
        [InlineData(@"some\other\path")]
        [InlineData(@"some\other\path\")]
        [InlineData(@".\some\other\path")]
        [InlineData(@".\some\other\path\")]
        [InlineData(@".\foo\..\some\other\path")]
        [InlineData(@".\foo\..\some\other\path\")]
        public void CanGetRelativePath(string unadjustedPath)
        {
            var relativeTo = AdjustToCurrentOs(@"root\dir");
            var path = AdjustToCurrentOs(unadjustedPath);
            
            var relativePath = PathPolyfill.GetRelativePath(relativeTo, path);

            relativePath
                .Should()
                .Be(AdjustToCurrentOs(@"..\..\some\other\path"));

            relativePath
                .Should()
                .Be(Path.GetRelativePath(relativeTo, path));
        }

        [Theory]
        [InlineData(@"root\dir")]
        [InlineData(@"root\dir\")]
        [InlineData(".")]
        [InlineData(@".\")]
        [InlineData(@".\foo\..")]
        [InlineData(@".\foo\..\")]
        public void CanGetEmptyRelativePath(string unadjustedPath)
        {
            var path = AdjustToCurrentOs(unadjustedPath);

            var relativePath = PathPolyfill.GetRelativePath(path, path);

            relativePath
                .Should()
                .Be(".");

            relativePath
                .Should()
                .Be(Path.GetRelativePath(path, path));
        }

        private static string AdjustToCurrentOs(string path) => path
            .Split(@"\")
            .Aggregate(Path.Combine);
    }
}
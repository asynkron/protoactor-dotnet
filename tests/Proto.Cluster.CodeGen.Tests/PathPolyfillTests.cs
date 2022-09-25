// -----------------------------------------------------------------------
// <copyright file="PathPolyfillTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Proto.Cluster.CodeGen.Tests;

public class PathPolyfillTests
{
    [Theory]
    [InlineData(@"./..", @"./..")]
    [InlineData(@"same", @"same")]
    [InlineData(@"..\dir", @"some\other\path")]
    [InlineData(@".\dir", @"some\other\path")]
    [InlineData(@".\root\dir", @"some\other\path")]
    [InlineData(@"root\dir", @"some\other\path")]
    [InlineData(@"root\dir", @"some\other\path.dot")]
    [InlineData(@"root\dir", @"some\other\path\")]
    [InlineData(@"root\dir", @".\some\other\path")]
    [InlineData(@"root\dir\", @".\some\other\path")]
    [InlineData(@"root\dir", @".\some\other\path\")]
    [InlineData(@"root\dir", @".\foo\..\some\other\path")]
    [InlineData(@"root\dir", @".\foo\..\some\other\path\")]
    [InlineData(@"root\dir.something\else", @"some\other\path")]
    [InlineData(@"root\dir.some.thing", @"some\other\path")]
    [InlineData(@"root\dir.something", @"some\other\path")]
    [InlineData(@"root\dir.something\", @"some\other\path")]
    public void CanGetRelativePath(string basePath, string unadjustedPath)
    {
        var relativeTo = AdjustToCurrentOs(basePath);
        var path = AdjustToCurrentOs(unadjustedPath);

        var relativePath = PathPolyfill.GetRelativePath(relativeTo, path);

        var expected = Path.GetRelativePath(relativeTo, path);

        relativePath
            .Should()
            .Be(expected, "Should match the Path.GetRelativePath poly-filled behavior");
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

    private static string AdjustToCurrentOs(string path) =>
        path
            .Split(@"\")
            .Aggregate(Path.Combine);
}
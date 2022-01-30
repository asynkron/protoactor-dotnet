// -----------------------------------------------------------------------
// <copyright file="Polyfills.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.IO;

namespace Proto.Cluster.CodeGen
{
    static class PathPolyfill
    {
        // this substitutes Path.GetRelativePath, which is not available in .NET Standard 2.0
        // code is based on Stack Overflow answer: https://stackoverflow.com/a/32113484
        // which was written by: https://stackoverflow.com/users/1212017/muhammad-rehan-saeed
        public static string GetRelativePath(string relativeTo, string path)
        {
            if (string.IsNullOrEmpty(relativeTo))
            {
                throw new ArgumentException("value cannot be null or empty", nameof(relativeTo));
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("value cannot be null or empty", nameof(path));
            }

            var relativeToUri = GetUri(relativeTo.EndsWith(Path.DirectorySeparatorChar.ToString())? relativeTo : relativeTo+Path.DirectorySeparatorChar);

            var pathUri = GetUri(path);

            if (relativeToUri.Scheme != pathUri.Scheme)
            {
                return path;
            }

            var relativeUri = relativeToUri.MakeRelativeUri(pathUri);

            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (string.Equals(pathUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            relativePath = relativePath.TrimEnd(Path.DirectorySeparatorChar);

            if (relativePath == string.Empty)
            {
                relativePath = ".";
            }

            return relativePath;
        }

        private static Uri GetUri(string path)
        {
            var fullPath = Path.GetFullPath(path+Path.DirectorySeparatorChar);
            var pathWithDirectorySeparatorChar = AppendDirectorySeparatorChar(fullPath);

            return new Uri(pathWithDirectorySeparatorChar);
        }

        private static string AppendDirectorySeparatorChar(string path)
        {
            // Append a slash only if the path is a directory and does not have a slash.
            if (!Path.HasExtension(path) && !path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return path + Path.DirectorySeparatorChar;
            }

            return path;
        }
    }
}
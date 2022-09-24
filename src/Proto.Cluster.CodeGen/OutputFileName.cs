// -----------------------------------------------------------------------
// <copyright file="OutputFileName.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Proto.Cluster.CodeGen;

public static class OutputFileName
{
    public static string GetOutputFileName(FileInfo inputFile, FileInfo? templateFile = null)
    {
        var baseName = Path.GetFileNameWithoutExtension(inputFile.Name);

        // hashcode is generated to avoid duplicate output file names, but also so file
        // names are deterministic (random output file names break in Visual Studio)
        var hash = GetHash(inputFile, templateFile);

        return $"{baseName}-{hash}.cs";
    }

    private static string GetHash(FileInfo inputFile, FileInfo? templateFile)
    {
        // MD5 is used as .GetHashCode() is not deterministic between runs
        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);

        incrementalHash.AppendData(
            Encoding.Unicode.GetBytes(inputFile.FullName)
        );

        if (templateFile is not null)
        {
            incrementalHash.AppendData(
                Encoding.Unicode.GetBytes(templateFile.FullName)
            );
        }

        var hashCodeFormattedBytes = incrementalHash
            .GetHashAndReset()
            .Select(@byte => @byte.ToString("X2", CultureInfo.InvariantCulture));

        return string.Concat(hashCodeFormattedBytes);
    }
}
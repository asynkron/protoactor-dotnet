// -----------------------------------------------------------------------
// <copyright file="LogStoreEntry.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto.Logging
{
    [PublicAPI]
    public record LogStoreEntry(
        int Index,
        DateTimeOffset Timestamp,
        LogLevel LogLevel,
        string Category,
        string Template,
        Exception? Exception,
        object[] Args
    )
    {
        public bool IsBefore(LogStoreEntry other) => Index < other.Index;

        public bool IsAfter(LogStoreEntry other) => Index > other.Index;

        public string ToFormattedString()
        {
            var formatter = new LogValuesFormatter(Template);
            var str = formatter.Format(Args);
            return $"[{Timestamp:hh:mm:ss.fff}] [{Category}][{LogLevel}] {str} {Exception}";
        }
    }
}
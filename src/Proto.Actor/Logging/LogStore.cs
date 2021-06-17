// -----------------------------------------------------------------------
// <copyright file="LogStore.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto.Logging
{
    [PublicAPI]
    public class LogStore
    {
        private readonly object _lock = new();
        private readonly List<LogStoreEntry> _entries = new();

        public void Append(LogLevel logLevel, string category, string template, Exception? x, object[] args)
        {
            lock (_lock)
            {
                _entries.Add(new LogStoreEntry(_entries.Count, DateTimeOffset.Now, logLevel, category, template,x, args));
            }
        }

        public IReadOnlyList<LogStoreEntry> GetEntries()
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }

        public LogStoreEntry? FindEntry(string partialTemplate)
        {
            lock (_lock)
            {
                var entry = GetEntries().FirstOrDefault(e => e.Template.Contains(partialTemplate, StringComparison.InvariantCulture));
                return entry;
            }
        }
        
        public LogStoreEntry? FindLastEntry(string partialTemplate)
        {
            lock (_lock)
            {
                var entry = GetEntries().LastOrDefault(e => e.Template.Contains(partialTemplate, StringComparison.InvariantCulture));
                return entry;
            }
        }

        public LogStoreEntry? FindEntryByCategory(string partialCategory, string partialTemplate)
        {
            lock (_lock)
            {
                var entry = GetEntries().FirstOrDefault(e => e.Category.Contains(partialCategory, StringComparison.InvariantCulture) && e.Template.Contains(partialTemplate, StringComparison.InvariantCulture));
                return entry;
            }
        }
        
        public LogStoreEntry? FindLastEntryByCategory(string partialCategory, string partialTemplate)
        {
            lock (_lock)
            {
                var entry = GetEntries().LastOrDefault(e => e.Category.Contains(partialCategory, StringComparison.InvariantCulture) && e.Template.Contains(partialTemplate, StringComparison.InvariantCulture));
                return entry;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
            }
        }


        public string ToFormattedString()
        {
            var sb = new StringBuilder();
            var entries = GetEntries();

            foreach (var entry in entries)
            {
                var str = entry.ToFormattedString();
                sb.AppendLine(str);
            }

            return sb.ToString();
        }
    }
}
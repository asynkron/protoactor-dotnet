// -----------------------------------------------------------------------
// <copyright file="InstanceLogger.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Extensions;

namespace Proto.Logging
{
    [PublicAPI]
    public record LogStoreEntry(int Index, LogLevel LogLevel, string Template, object[] args)
    {
        public bool IsBefore(LogStoreEntry other) => Index < other.Index;
        public bool IsAfter(LogStoreEntry other) => Index > other.Index;
    }

    [PublicAPI]
    public class LogStore
    {
        private readonly List<LogStoreEntry> _entries = new();

        public void Append(LogLevel logLevel, string format, object[] args)
        {
            lock (this)
            {
                _entries.Add(new LogStoreEntry(_entries.Count, logLevel, format, args));
            }
        }

        public IReadOnlyList<LogStoreEntry> GetEntries() => _entries.ToList();


        public LogStoreEntry? FindEntry(string partialTemplate)
        {
            lock (this)
            {
                var entry = GetEntries().FirstOrDefault(e => e.Template.Contains(partialTemplate));
                return entry;
            }
        }
        
        
     }
    

    [PublicAPI]
    public class InstanceLogger : IActorSystemExtension<InstanceLogger>
    {
        private readonly LogLevel _logLevel;
        private readonly ILogger? _logger;
        private readonly LogStore? _logStore;
        

        public InstanceLogger(LogLevel logLevel, LogStore? logStore = null  , ILogger? logger = null)
        {
            _logLevel = logLevel;
            _logger = logger;
            _logStore = logStore;
        }

        public void LogDebug(string template, params object[] args)
        {
            if (_logLevel > LogLevel.Debug)
                return;

            _logger?.LogDebug(template, args);
            _logStore?.Append(_logLevel,template,args);
            
        }

        public void LogDebug(Exception x, string template, params object[] args)
        {
            if (_logLevel > LogLevel.Debug)
                return;

            _logger?.LogDebug(x, template, args);
            _logStore?.Append(_logLevel,template,args);
        }

        public void LogInformation(string template, params object[] args)
        {
            if (_logLevel > LogLevel.Information)
                return;

            _logger?.LogInformation(template, args);
            _logStore?.Append(_logLevel,template,args);
        }

        public void LogInformation(Exception x, string template, params object[] args)
        {
            if (_logLevel > LogLevel.Information)
                return;

            _logger?.LogDebug(x, template, args);
            _logStore?.Append(_logLevel,template,args);
        }

        public void LogWarning(string template, params object[] args)
        {
            if (_logLevel > LogLevel.Warning)
                return;

            _logger?.LogWarning(template, args);
            _logStore?.Append(_logLevel,template,args);
        }

        public void LogWarning(Exception x, string template, params object[] args)
        {
            if (_logLevel > LogLevel.Warning)
                return;

            _logger?.LogWarning(x, template, args);
            _logStore?.Append(_logLevel,template,args);
        }

        public void LogError(string template, params object[] args)
        {
            if (_logLevel > LogLevel.Error)
                return;

            _logger?.LogError(template, args);
            _logStore?.Append(_logLevel,template,args);
        }

        public void LogError(Exception x, string template, params object[] args)
        {
            if (_logLevel > LogLevel.Error)
                return;

            _logger?.LogError(x, template, args);
            _logStore?.Append(_logLevel,template,args);
        }

        public void LogCritical(string template, params object[] args)
        {
            if (_logLevel > LogLevel.Critical)
                return;

            _logger?.LogCritical(template, args);
            _logStore?.Append(_logLevel,template,args);
        }

        public void LogCritical(Exception x, string template, params object[] args)
        {
            if (_logLevel > LogLevel.Critical)
                return;

            _logger?.LogCritical(x, template, args);
            _logStore?.Append(_logLevel,template,args);
        }
    }
}
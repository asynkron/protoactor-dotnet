// -----------------------------------------------------------------------
// <copyright file="InstanceLogger.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Extensions;

namespace Proto.Logging
{
    [PublicAPI]
    public class InstanceLogger : IActorSystemExtension<InstanceLogger>
    {
        private readonly LogLevel _logLevel;
        private readonly ILogger? _logger;
        private readonly LogStore? _logStore;
        private readonly string _category;

        public InstanceLogger BeginMethodScope([CallerMemberName]string caller="")  => new(_logLevel, _logStore, _logger, $"{_category}/{caller}");
        public InstanceLogger BeginScope<T>()  => new(_logLevel, _logStore, _logger, typeof(T).Name);
        public InstanceLogger BeginScope(string category) => new(_logLevel, _logStore, _logger, $"{_category}/{category}");

        public InstanceLogger(LogLevel logLevel, LogStore? logStore = null, ILogger? logger = null, string category = "default")
        {
            _logLevel = logLevel;
            _logger = logger;
            _logStore = logStore;
            _category = category;
        }

        public void LogDebug(string template, params object[] args)
        {
            if (_logLevel > LogLevel.Debug)
                return;

            _logger?.LogDebug(template, args);
            _logStore?.Append(_logLevel, _category, template,null, args);

        }

        public void LogDebug(Exception x, string template, params object[] args)
        {
            if (_logLevel > LogLevel.Debug)
                return;

            _logger?.LogDebug(x, template, args);
            _logStore?.Append(_logLevel, _category, template,x, args);
        }

        public void LogInformation(string template, params object[] args)
        {
            if (_logLevel > LogLevel.Information)
                return;

            _logger?.LogInformation(template, args);
            _logStore?.Append(_logLevel, _category, template,null, args);
        }

        public void LogInformation(Exception x, string template, params object[] args)
        {
            if (_logLevel > LogLevel.Information)
                return;

            _logger?.LogInformation(x, template, args);
            _logStore?.Append(_logLevel, _category, template,x, args);
        }

        public void LogWarning(string template, params object[] args)
        {
            if (_logLevel > LogLevel.Warning)
                return;

            _logger?.LogWarning(template, args);
            _logStore?.Append(_logLevel, _category, template,null, args);
        }

        public void LogWarning(Exception x, string template, params object[] args)
        {
            if (_logLevel > LogLevel.Warning)
                return;

            _logger?.LogWarning(x, template, args);
            _logStore?.Append(_logLevel, _category, template,x, args);
        }

        public void LogError(string template, params object[] args)
        {
            if (_logLevel > LogLevel.Error)
                return;

            _logger?.LogError(template, args);
            _logStore?.Append(_logLevel, _category, template,null, args);
        }

        public void LogError(Exception x, string template, params object[] args)
        {
            if (_logLevel > LogLevel.Error)
                return;

            _logger?.LogError(x, template, args);
            _logStore?.Append(_logLevel, _category, template,x, args);
        }

        public void LogCritical(string template, params object[] args)
        {
            if (_logLevel > LogLevel.Critical)
                return;

            _logger?.LogCritical(template, args);
            _logStore?.Append(_logLevel, _category, template,null, args);
        }

        public void LogCritical(Exception x, string template, params object[] args)
        {
            if (_logLevel > LogLevel.Critical)
                return;

            _logger?.LogCritical(x, template, args);
            _logStore?.Append(_logLevel, _category, template,x, args);
        }
    }
}
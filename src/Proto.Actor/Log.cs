// -----------------------------------------------------------------------
//   <copyright file="Log.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace Proto
{
    public static class Log
    {
        private static ILoggerFactory loggerFactory = new NullLoggerFactory();

        public static void SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            Log.loggerFactory = loggerFactory;
        }

        public static ILogger CreateLogger(string categoryName)
        {
            return loggerFactory.CreateLogger(categoryName);
        }

        public static ILogger CreateLogger<T>()
        {
            return loggerFactory.CreateLogger<T>();
        }
    }
}
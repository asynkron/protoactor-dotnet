// -----------------------------------------------------------------------
// <copyright file="CancellationTokens.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Proto
{
    
    public static class CancellationTokens
    {
        private record TokenEntry(DateTimeOffset Timestamp, CancellationToken Token);
        
        private static readonly ConcurrentDictionary<int, TokenEntry> Tokens = new();

        public static CancellationToken FromSeconds(int seconds)
        {
            if (seconds < 1) throw new ArgumentOutOfRangeException(nameof(seconds));
            
            static TokenEntry ValueFactory(int seconds)
            {
                var cts = new CancellationTokenSource(seconds * 1000);
                return new TokenEntry(DateTimeOffset.Now, cts.Token);
            }

            while (true)
            {
                var x = Tokens.GetOrAdd(seconds, ValueFactory);

                //is the entry expired?
                //token reuse expire after 500ms
                if (x.Timestamp >= DateTimeOffset.Now.AddMilliseconds(-500))
                {
                    return x.Token;
                }

                Tokens.TryRemove(seconds, out _);
            }
        }
        [Obsolete("Use and dispose CancellationTokenSource, or use CancellationTokens.FromSeconds",true)]
        public static CancellationToken WithTimeout(int ms) => new CancellationTokenSource(ms).Token;
        public static CancellationToken WithTimeout(TimeSpan timeSpan) => new CancellationTokenSource(timeSpan).Token;
    }
}
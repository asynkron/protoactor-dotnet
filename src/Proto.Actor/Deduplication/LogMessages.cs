// // Copyright (c) Dolittle. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Proto.Deduplication;

static partial class LogMessages
{
    // I'm of the opinion that this should be Debug, not Information
    [LoggerMessage(0, LogLevel.Information, "Request de-duplicated")]
    internal static partial void RequestDeDuplicated(this ILogger logger);
}
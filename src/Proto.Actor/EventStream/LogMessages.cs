// // Copyright (c) Dolittle. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Proto;

static partial class LogMessages
{
    [LoggerMessage(0, LogLevel.Debug, "Setting expectation")]
    internal static partial void ProbeSettingExpectation(this ILogger logger);

    [LoggerMessage(1, LogLevel.Debug, "Got expected event {TheEvent}")]
    internal static partial void ProbeGotExpected(this ILogger logger, object theEvent);

    [LoggerMessage(2, LogLevel.Debug, "Got unexpected event {TheEvent}, ignoring")]
    internal static partial void ProbeGotUnexpected(this ILogger logger, object theEvent);
}
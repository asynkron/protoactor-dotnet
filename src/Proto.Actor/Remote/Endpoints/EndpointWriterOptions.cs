using System;

namespace Proto.Remote;

public class EndpointWriterOptions
{
    /// <summary>
    ///     Gets or sets the batch size for the endpoint writer. The default value is 1000.
    ///     The endpoint writer will send up to this number of messages in a batch.
    /// </summary>
    public int EndpointWriterBatchSize { get; set; } = 1000;

    /// <summary>
    ///     The number of times to retry the connection within the RetryTimeSpan, default is 8.
    /// </summary>
    public int MaxRetries { get; set; } = 8;

    /// <summary>
    ///     The timespan that restarts are counted within, meaning that the retry counter resets after this timespan if no
    ///     errors.
    ///     The default value is 3 minutes.
    /// </summary>
    public TimeSpan RetryTimeSpan { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    ///     Each retry backs off by an exponential ratio of this timespan
    ///     The default value is 100ms.
    /// </summary>
    public TimeSpan RetryBackOff { get; set; } = TimeSpan.FromMilliseconds(100);
}
using System;

namespace Proto.Remote
{
    public class EndpointWriterOptions
    {
        /// <summary>
        ///     Gets or sets the batch size for the endpoint writer. The default value is 1000.
        ///     The endpoint writer will send up to this number of messages in a batch.
        /// </summary>
        public int EndpointWriterBatchSize { get; set; } = 1000;

        /// <summary>
        ///     the number of times to retry the connection within the RetryTimeSpan
        /// </summary>
        public int MaxRetries { get; set; } = 8;

        /// <summary>
        ///     the timespan that restarts are counted withing.
        ///     meaning that the retry counter resets after this timespan if no errors.
        /// </summary>
        public TimeSpan RetryTimeSpan { get; set; } = TimeSpan.FromMinutes(3);

        /// <summary>
        ///     each retry backs off by an exponential ratio of this timespan
        /// </summary>
        public TimeSpan RetryBackOff { get; set; } = TimeSpan.FromMilliseconds(100);
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.TestKit
{
    /// <summary>
    /// a test probe for intercepting messages
    /// </summary>
    public interface ITestProbe
    {
        /// <summary>
        /// the sender of the last message retrieved from GetNextMessage or FishForMessage
        /// </summary>
        PID Sender { get; }

        /// <summary>
        /// the context of the test probe
        /// </summary>
        IContext Context { get; }

        /// <summary>
        /// this method will throw an exception if the probe receives a message within the time allowed
        /// </summary>
        /// <param name="timeAllowed"></param>
        void ExpectNoMessage(TimeSpan? timeAllowed = null);

        /// <summary>
        /// gets the next message from the test probe
        /// </summary>
        /// <param name="timeAllowed"></param>
        /// <returns></returns>
        object GetNextMessage(TimeSpan? timeAllowed = null);

        /// <summary>
        /// gets the next message from the test probe
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="timeAllowed"></param>
        /// <returns></returns>
        T GetNextMessage<T>(TimeSpan? timeAllowed = null);

        /// <summary>
        /// gets the next message from the test probe
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="when"></param>
        /// <param name="timeAllowed"></param>
        /// <returns></returns>
        T GetNextMessage<T>(Func<T, bool> when, TimeSpan? timeAllowed = null);

        /// <summary>
        /// keeps returning messages until the interval between messages exceeds the time allowed
        /// </summary>
        /// <param name="timeAllowed"></param>
        /// <returns></returns>
        IEnumerable ProcessMessages(TimeSpan? timeAllowed = null);

        /// <summary>
        /// keeps returning messages until the interval between messages exceeds the time allowed
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="timeAllowed"></param>
        /// <returns></returns>
        IEnumerable<T> ProcessMessages<T>(TimeSpan? timeAllowed = null);

        /// <summary>
        /// keeps returning messages until the interval between messages exceeds the time allowed
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="when"></param>
        /// <param name="timeAllowed"></param>
        /// <returns></returns>
        IEnumerable<T> ProcessMessages<T>(Func<T, bool> when, TimeSpan? timeAllowed = null);

        /// <summary>
        /// fishes for the next message of a given type from the test probe
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="timeAllowed"></param>
        /// <returns></returns>
        T FishForMessage<T>(TimeSpan? timeAllowed = null);

        /// <summary>
        /// fishes for the next message of a given type from the test probe
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="when"></param>
        /// <param name="timeAllowed"></param>
        /// <returns></returns>
        T FishForMessage<T>(Func<T, bool> when, TimeSpan? timeAllowed = null);

        /// <summary>
        /// sends a message from the test probe to the target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="message"></param>
        void Send(PID target, object message);

        /// <summary>
        /// responds to the current sender
        /// </summary>
        /// <param name="message"></param>
        void Respond(object message);

        /// <summary>
        /// sends a request message from the test probe to the target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="message"></param>
        void Request(PID target, object message);

        /// <summary>
        /// requests a message from the target
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="target"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        Task<T> RequestAsync<T>(PID target, object message);

        /// <summary>
        /// requests a message from the target
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="target"></param>
        /// <param name="message"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken);

        /// <summary>
        /// requests a message from the target
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="target"></param>
        /// <param name="message"></param>
        /// <param name="timeAllowed"></param>
        /// <returns></returns>
        Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeAllowed);
    }
}
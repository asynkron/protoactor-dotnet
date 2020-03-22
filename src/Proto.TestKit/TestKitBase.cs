using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.TestKit
{
    /// <summary>
    /// general purpose testing base class
    /// </summary>
    public abstract class TestKitBase : ITestProbe, ISpawnerContext
    {
        /// <inheritdoc />
        public PID Sender => Probe.Sender;

        /// <inheritdoc />
        public IContext Context => Probe.Context;

        /// <summary>
        /// the underlying test probe
        /// </summary>
        public TestProbe Probe { get; set; }

        /// <summary>
        /// sets up the test environment
        /// </summary>
        public virtual void SetUp() => Probe = TestKit.System.Root.Spawn(Props.FromProducer(() => new TestProbe()));

        /// <summary>
        /// tears down the test environment
        /// </summary>
        public virtual void TearDown()
        {
            Context.Stop(Context.Self);
            Probe = null;
        }

        /// <inheritdoc />
        public void ExpectNoMessage(TimeSpan? timeAllowed = null) => Probe.ExpectNoMessage(timeAllowed);

        /// <inheritdoc />
        public object GetNextMessage(TimeSpan? timeAllowed = null) => Probe.GetNextMessage(timeAllowed);

        /// <inheritdoc />
        public T GetNextMessage<T>(TimeSpan? timeAllowed = null) => Probe.GetNextMessage<T>(timeAllowed);

        /// <inheritdoc />
        public T GetNextMessage<T>(Func<T, bool> when, TimeSpan? timeAllowed = null) =>
            Probe.GetNextMessage(when, timeAllowed);

        /// <inheritdoc />
        public IEnumerable ProcessMessages(TimeSpan? timeAllowed = null) => Probe.ProcessMessages(timeAllowed);

        /// <inheritdoc />
        public IEnumerable<T> ProcessMessages<T>(TimeSpan? timeAllowed = null) => Probe.ProcessMessages<T>(timeAllowed);

        /// <inheritdoc />
        public IEnumerable<T> ProcessMessages<T>(Func<T, bool> when, TimeSpan? timeAllowed = null) =>
            Probe.ProcessMessages(when, timeAllowed);

        /// <inheritdoc />
        public T FishForMessage<T>(TimeSpan? timeAllowed = null) => Probe.FishForMessage<T>(timeAllowed);

        /// <inheritdoc />
        public T FishForMessage<T>(Func<T, bool> when, TimeSpan? timeAllowed = null) =>
            Probe.FishForMessage(when, timeAllowed);

        /// <inheritdoc />
        public void Send(PID target, object message) => Probe.Send(target, message);

        /// <inheritdoc />
        public void Request(PID target, object message) => Probe.Request(target, message);

        /// <inheritdoc />
        public void Respond(object message) => Probe.Respond(message);

        /// <inheritdoc />
        public Task<T> RequestAsync<T>(PID target, object message) => Probe.RequestAsync<T>(target, message);

        /// <inheritdoc />
        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken) =>
            Probe.RequestAsync<T>(target, message, cancellationToken);

        /// <inheritdoc />
        public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeAllowed) =>
            Probe.RequestAsync<T>(target, message, timeAllowed);

        /// <summary>
        /// creates a test probe
        /// </summary>
        /// <returns></returns>
        public TestProbe CreateTestProbe() => Context.Spawn(Props.FromProducer(() => new TestProbe()));

        /// <summary>
        ///     Spawns a new child actor based on props and named with a unique ID.
        /// </summary>
        /// <param name="producer"></param>
        /// <returns></returns>
        public PID Spawn(Producer producer) => Context.Spawn(Props.FromProducer(producer));

        /// <summary>
        ///     Spawns a new child actor based on props and named with a unique ID.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public PID Spawn<T>() where T : IActor, new() => Context.Spawn(Props.FromProducer(() => new T()));

        /// <inheritdoc />
        public PID Spawn(Props props) => Context.Spawn(props);

        /// <summary>
        ///     Spawns a new child actor based on props and named using the specified name.
        /// </summary>
        /// <param name="producer"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public PID SpawnNamed(Producer producer, string name) =>
            Context.SpawnNamed(Props.FromProducer(producer), name);

        /// <summary>
        ///     Spawns a new child actor based on props and named using the specified name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        public PID SpawnNamed<T>(string name) where T : IActor, new() =>
            Context.SpawnNamed(Props.FromProducer(() => new T()), name);

        /// <inheritdoc />
        public PID SpawnNamed(Props props, string name) => Context.SpawnNamed(props, name);

        /// <summary>
        ///     Spawns a new child actor based on props and named using a prefix followed by a unique ID.
        /// </summary>
        /// <param name="producer"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public PID SpawnPrefix(Producer producer, string prefix) =>
            Context.SpawnPrefix(Props.FromProducer(producer), prefix);

        /// <summary>
        ///     Spawns a new child actor based on props and named using a prefix followed by a unique ID.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public PID SpawnPrefix<T>(string prefix) where T : IActor, new() =>
            Context.SpawnPrefix(Props.FromProducer(() => new T()), prefix);

        /// <inheritdoc />
        public PID SpawnPrefix(Props props, string prefix) => Context.SpawnPrefix(props, prefix);
    }
}
// -----------------------------------------------------------------------
// <copyright file="TestKitBase.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.TestKit;

/// <summary>
///     General purpose testing base class
/// </summary>
[PublicAPI]
public class TestKitBase : ITestProbe, ISpawnerContext
{
    private readonly ActorSystem? _providedSystem;
    private readonly ActorSystemConfig? _config;
    private bool _ownsSystem;
    private TestProbe? _probe;

    /// <summary>
    ///     Creates a new instance. A fresh <see cref="ActorSystem"/> will be created during <see cref="SetUp"/>.
    /// </summary>
    public TestKitBase()
    {
    }

    /// <summary>
    ///     Uses the provided <see cref="ActorSystem"/> instead of creating one.
    /// </summary>
    /// <param name="system">An existing actor system.</param>
    public TestKitBase(ActorSystem system)
    {
        _providedSystem = system;
    }

    /// <summary>
    ///     Uses the specified configuration when creating a new <see cref="ActorSystem"/> during <see cref="SetUp"/>.
    /// </summary>
    /// <param name="config">Configuration for the actor system.</param>
    public TestKitBase(ActorSystemConfig config)
    {
        _config = config;
    }

    /// <inheritdoc />
    public ActorSystem System { get; private set; } = null!;

    /// <summary>
    ///     the underlying test probe
    /// </summary>
    public TestProbe Probe
    {
        get
        {
            if (_probe is null)
            {
                throw new TestKitException("Probe hasn't been set up");
            }

            return _probe;
        }
        private set => _probe = value;
    }

    /// <inheritdoc />
    public PID SpawnNamed(Props props, string name, Action<IContext>? callback = null) =>
        Context.SpawnNamed(props, name, callback);

    /// <inheritdoc />
    public PID? Sender => Probe?.Sender;

    /// <inheritdoc />
    public IContext Context => Probe.Context;

    /// <inheritdoc />
    public void ExpectNoMessage(TimeSpan? timeAllowed = null) => Probe.ExpectNoMessage(timeAllowed);

    /// <inheritdoc />
    public object? GetNextMessage(TimeSpan? timeAllowed = null) => Probe.GetNextMessage(timeAllowed);

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

    /// <inheritdoc />
    public PID Spawn(Props props) => Context.Spawn(props);

    /// <inheritdoc />
    public PID SpawnPrefix(Props props, string prefix) => Context.SpawnPrefix(props, prefix);

    /// <summary>
    ///     Sets up the test environment and creates a dedicated <see cref="ActorSystem"/> if needed.
    /// </summary>
    public virtual void SetUp()
    {
        if (_providedSystem is not null)
        {
            System = _providedSystem;
            _ownsSystem = false;
        }
        else
        {
            System = new ActorSystem(_config ?? new ActorSystemConfig());
            _ownsSystem = true;
        }

        var pid = System.Root.Spawn(Props.FromProducer(() => new TestProbe()));
        Probe = TestProbe.FromPid(System, pid)!;
    }

    /// <summary>
    ///     tears down the test environment
    /// </summary>
    public virtual void TearDown()
    {
        if (Context?.Self is not null)
        {
            Context.Stop(Context.Self);
        }

        _probe = null;

        if (_ownsSystem)
        {
            System.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    ///     creates a test probe
    /// </summary>
    /// <returns></returns>
    public TestProbe CreateTestProbe()
    {
        var pid = Context.Spawn(Props.FromProducer(() => new TestProbe()));
        return TestProbe.FromPid(System, pid)!;
    }

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

    /// <summary>
    ///     Spawns a new child actor based on props and named using the specified name.
    /// </summary>
    /// <param name="producer"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public PID SpawnNamed(Producer producer, string name) => Context.SpawnNamed(Props.FromProducer(producer), name);

    /// <summary>
    ///     Spawns a new child actor based on props and named using the specified name.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name"></param>
    /// <returns></returns>
    public PID SpawnNamed<T>(string name) where T : IActor, new() =>
        Context.SpawnNamed(Props.FromProducer(() => new T()), name);

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

    /// <summary>
    ///     Retrieves a <see cref="TestProbe"/> from a <see cref="PID"/> within this test's actor system.
    /// </summary>
    /// <param name="pid">PID of the probe actor.</param>
    /// <returns>The <see cref="TestProbe"/> instance or <c>null</c> if not found.</returns>
    public TestProbe? GetTestProbe(PID pid) => TestProbe.FromPid(System, pid);
}

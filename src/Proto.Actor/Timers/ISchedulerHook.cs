namespace Proto.Timers;

/// <summary>
/// Hook for tests to observe scheduler behavior.
/// </summary>
public interface ISchedulerHook
{
    /// <summary>
    /// Called whenever the scheduler registers an internal timer.
    /// </summary>
    void OnTimerRegistered();
}


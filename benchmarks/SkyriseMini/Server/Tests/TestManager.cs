namespace SkyriseMini.Tests;

public class TestManager
{
    Task _currentTest = Task.CompletedTask;
    CancellationTokenSource _cts = new();
    readonly object _syncRoot = new();

    public void TrackTest(Func<CancellationToken, Task> runTest)
    {
        lock (_syncRoot)
        {
            if (TestIsCurrentlyRunning())
                throw new Exception("Test is currently running, cannot run another one");

            _cts = new();
            _currentTest = runTest(_cts.Token);
        }
    }

    private bool TestIsCurrentlyRunning() => !_currentTest.IsCompleted;
}
using System;
using BenchmarkDotNet.Attributes;
using ThrottleBenchmarks.Legacy;

namespace ThrottleBenchmarks;

[MemoryDiagnoser]
public class ThrottleBenchmarks
{
    private ShouldThrottle _new = null!;
    private ShouldThrottle _old = null!;

    [Params(10)]
    public int Limit;

    [GlobalSetup]
    public void Setup()
    {
        // verify behaviour equivalence
        var testNew = Throttle.Create(Limit, TimeSpan.FromSeconds(1));
        var testOld = LegacyThrottle.Create(Limit, TimeSpan.FromSeconds(1));
        for (var i = 0; i < Limit * 2; i++)
        {
            if (testNew() != testOld())
            {
                throw new InvalidOperationException("Implementations differ");
            }
        }

        _new = Throttle.Create(Limit, TimeSpan.FromSeconds(1));
        _old = LegacyThrottle.Create(Limit, TimeSpan.FromSeconds(1));
    }

    [Benchmark]
    public Throttle.Valve NewThrottle() => _new();

    [Benchmark]
    public Throttle.Valve OldThrottle() => _old();
}

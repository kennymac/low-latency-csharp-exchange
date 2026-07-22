namespace LowLatency.Profiler.UnitTests;

public class ProfilerUnitTest
{
    [Fact]
    public void OrderExecution_MustBeZeroAllocation()
    {
        // 1. Warm-up phase: JIT-compile all paths and initialize static buffers
        var processProfiler = new ProcessProfiler();
        processProfiler.MeasureAllocationsInChildProcess(
            "/Users/studio/Code/low-latency-scratchpad/LowLatency.ScratchPad.Engine/bin/Debug/net10.0/LowLatency.ScratchPad.Engine.dll");
    }
}
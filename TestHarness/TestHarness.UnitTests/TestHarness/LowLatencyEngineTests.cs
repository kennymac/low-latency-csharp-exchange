using LowLatency.ScratchPad;
using LowLatency.ScratchPad.Engine;

namespace TestHarness.UnitTests.TestHarness;

public class LowLatencyEngineTests
{
    [Fact]
    public void OrderExecution_MustBeZeroAllocation()
    {
        // 1. Warm-up phase: JIT-compile all paths and initialize static buffers
        var engine = new MatchingEngine();
        engine.ExecuteOrder(101, 150.50m, 100);

        // 2. Force GC to establish a clean baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 3. Capture baseline
        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();

        // 4. Hot-Path Execution (The exact code you want to test)
        engine.ExecuteOrder(102, 150.55m, 200);

        // 5. Assert zero bytes allocated on managed heap
        long bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        long allocated = bytesAfter - bytesBefore;

        Assert.Equal(0, allocated);
    }
}
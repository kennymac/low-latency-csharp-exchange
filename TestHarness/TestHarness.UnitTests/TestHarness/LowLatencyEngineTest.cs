using LowLatency.ScratchPad.Engine;
using LowLatency.ScratchPad.Engine.Model;

namespace TestHarness.UnitTests.TestHarness;

public class LowLatencyEngineTest
{
    [Fact]
    public void OrderExecution_MustBeZeroAllocation()
    {
        // 1. Warm-up phase: JIT-compile all paths and initialize static buffers
        var engine = new MatchingEngine();
        engine.ProcessOrder(1, 101, 1, Side.Buy, 150, 100);

        // 2. Force GC to establish a clean baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 3. Capture baseline
        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();

        // 4. Hot-Path Execution (The exact code you want to test)
        engine.ProcessOrder(1, 102, 1, Side.Buy, 150, 200);

        // 5. Assert zero bytes allocated on the managed heap
        var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        var allocated = bytesAfter - bytesBefore;

        allocated.Should().Be(0, "no bytes should be allocated to the managed heap");
    }
}
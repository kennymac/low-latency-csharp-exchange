# automated, zero-allocation test harness

... you do not need heavy external tools or process-spawning to do it.

In modern .NET (.NET 7/8/9+), you can verify memory guarantees at three distinct levels: **unit-test assertions (in-process)**, **BenchmarkDotNet (CI/CD profiling)**, and **EventPipe/ETW tracing (out-of-process process spawning)**.

---

## 1. Direct Unit Test Assertions (In-Process)

The most direct way to assert zero allocations in an xUnit or NUnit test is using `GC.GetAllocatedBytesForCurrentThread()`.

Because C# tracks allocations per-thread, you can measure the delta before and after executing your hot path. If the delta is greater than zero, the test fails.

### The Zero-Allocation Test Pattern

```csharp
using Xunit;

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

```

> **Crucial Warning on Thread-Affinity:**
> Always execute the hot-path synchronously on a single thread. If your code spawns `Task.Run` or uses asynchronous thread pool context switches, allocations will jump to other threads. Keep your core low-latency domain engines strictly synchronous or thread-pinned.

---

## 2. Process Spawning & ETW / EventPipe Metrics

If you want to read runtime diagnostics using an external process runner (e.g., verifying native allocations, GC pause durations, or JIT compilation events), spawning a child process and monitoring its **EventPipe** or **ETW** stream is standard practice.

In modern cross-platform .NET, **`Microsoft.Diagnostics.NETCore.Client`** replaces legacy raw Windows ETW. It allows a host process (or your test runner) to spawn a child executable and stream real-time runtime events out-of-band.

### How to Build an EventPipe Test Harness

1. **Install NuGet Package:** `Microsoft.Diagnostics.NETCore.Client` and `Microsoft.Diagnostics.Tracing.TraceEvent`
2. **Spawn the Child Process:** Execute your test workload in a separate child process.
3. **Listen to GC / Allocation Events:**

```csharp
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

public class ProcessProfiler
{
    public static long MeasureAllocationsInChildProcess(string exePath)
    {
        var psi = new ProcessStartInfo(exePath) { RedirectStandardOutput = true };
        var process = Process.Start(psi);

        // Create an EventPipe session targeting the spawned process PID
        var providers = new[]
        {
            new EventPipeProvider("Microsoft-Windows-DotNETRuntime", 
                EventLevel.Verbose, 
                (long)ClrTraceEventParser.Keywords.GC) // Capture GC Allocation ticks
        };

        var client = new DiagnosticsClient(process.Id);
        using var session = client.StartEventPipeSession(providers, false);
        using var source = new EventPipeEventSource(session.EventStream);

        long totalBytesAllocated = 0;

        // Parse runtime allocation events in real-time
        source.Clr.GCAllocationTick += data =>
        {
            totalBytesAllocated += data.AllocationAmount;
        };

        // Run tracing in background while child process completes
        Task.Run(() => source.Process());
        process.WaitForExit();

        return totalBytesAllocated;
    }
}

```

---

## 3. Establishing a Complete Low-Latency Test Harness

To build a production-grade testing pipeline that covers both **zero-allocation guarantees** and **tail-latency bounds**, establish a three-tiered harness:

```
[ Tier 1: Unit Tests ]     ──> Fast check on every build: GC.GetAllocatedBytes == 0
[ Tier 2: BenchmarkDotNet] ──> Micro-benchmarks: Checks ns/op & [MemoryDiagnoser]
[ Tier 3: Harness Stress ] ──> Process spawn: 10M events, measures p99.99 Tail Latency

```

### Tier 1: xUnit Allocation Guard

Add custom XUnit attributes (e.g., `[ZeroAllocationFact]`) that wrap the thread byte-counting logic into reusable assertion attributes across your test suites.

### Tier 2: BenchmarkDotNet CI Regression Check

Use **BenchmarkDotNet** in your CI pipeline with the `[MemoryDiagnoser]` attribute. BDN automatically reads ETW/EventPipe data under the hood and yields precise metrics:

```csharp
[MemoryDiagnoser]
[DisassemblyDiagnoser(printSource: true)] // Verifies AVX2 / AVX-512 vectorization
public class MatchingEngineBenchmark
{
    private MatchingEngine _engine = new();

    [Benchmark]
    public void ProcessFill()
    {
        _engine.ExecuteOrder(101, 100.0m, 10);
    }
}

```

*If a PR introduces even a 24-byte allocation (e.g., an accidental boxing operation or lambda closure), `BenchmarkDotNet` reports `Allocated: 24 B` instead of `0 B`, failing the build.*

### Tier 3: Tail-Latency & Jitter Harness (p99 / p99.99)

For low latency, mean execution time is useless; **tail latency (p99.99)** is what matters.

Write a dedicated harness executable that:

1. Pin the thread to a specific CPU core (`ProcessThread.ProcessorAffinity`).
2. Run 100,000 "warm-up" iterations to ensure JIT tiering completes.
3. Fire 1,000,000 operations, recording latencies using `Stopwatch.GetTimestamp()` (which uses the high-precision hardware TSC counter).
4. Export a histogram (using `HdrHistogram.NET`) to verify your p99.99 tail latency stays within acceptable bounds (e.g., $< 5\,\mu\text{s}$).

This gives you an automated, unyielding system where no memory leaks, hidden GC pauses, or unexpected latency spikes can ever reach a production environment.
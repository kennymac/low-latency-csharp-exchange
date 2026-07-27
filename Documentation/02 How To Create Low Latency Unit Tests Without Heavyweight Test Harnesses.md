# How To Create Low-Latency Unit Tests Without Heavyweight Harnesses

This guide details how to verify zero-allocation guarantees and correctness directly inside lightweight unit tests (`xUnit`) without requiring heavyweight external process runners or complex profiling infrastructure.

---

## 1. The In-Process Zero-Allocation Test Pattern

In .NET (.NET 7/8/9/10+), the runtime tracks memory allocations per thread via `GC.GetAllocatedBytesForCurrentThread()`.

By measuring thread allocations before and after executing a hot path, we can assert **0 bytes allocated** directly inside an `xUnit` test.

### Core Test Template

```csharp
[Fact]
public void MethodNameOrWhen_GivenTheStateBeforeHand_ThenTheExpectedStateOrOutcome()
{
    // 1. Arrange & Warm-Up Phase: JIT-compile all execution paths and pre-allocate pools
    var engine = new MatchingEngine();
    engine.ProcessOrder(
        clientId: 1, 
        clientOrderId: 101, 
        tickerId: 1, 
        side: Side.Buy, 
        price: 150, 
        qty: 100);

    // 2. Force GC to establish a clean baseline
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    // 3. Capture baseline
    var bytesBefore = GC.GetAllocatedBytesForCurrentThread();

    // 4. Act: Hot-Path Execution
    engine.ProcessOrder(
        clientId: 1, 
        clientOrderId: 102, 
        tickerId: 1, 
        side: Side.Buy, 
        price: 150, 
        qty: 200);

    // 5. Assert zero bytes allocated on the managed heap
    var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
    var allocated = bytesAfter - bytesBefore;

    allocated.Should().Be(0, "no bytes should be allocated to the managed heap");
}
```

---

## 2. Key Rules for In-Process Allocation Testing

### A. Warm-Up Phase is Mandatory
Before measuring `bytesBefore`, execute at least 1 or 2 iterations of the exact path under test. This ensures:
* The JIT compiler completes compilation of all method branches.
* Static type constructors and array lookup structures complete initial allocations.
* Pre-allocated memory pools (`MemPool<T>`) warm up internal object arrays.

### B. Single-Threaded Context & Thread Affinity
`GC.GetAllocatedBytesForCurrentThread()` only tracks allocations made on the **calling thread**.
* Keep low-latency unit tests strictly synchronous on a single thread.
* If testing code that context-switches via `Task.Run` or thread pool background threads, allocations will escape the current thread's counter.

### C. Clean GC Baseline
Always call `GC.Collect()`, `GC.WaitForPendingFinalizers()`, and `GC.Collect()` after warm-up to ensure finalizers have run and the heap baseline is clean.

---

## 3. Sustained Bulk Allocation Testing

While single-execution tests verify individual hot paths, sustained bulk tests (e.g. 10,000 orders) are necessary to prove that memory pools don't leak or trigger array resizes over time.

```csharp
[Fact]
public void Add_GivenBulkOrdersFlow_ThenZeroGcAllocations()
{
    // Arrange
    var book = new OrderBook(tickerId: 1, clientResponseListener: null, marketUpdateListener: null);

    // Warm-up phase: run cycle to JIT compile methods and warm up internal memory pools
    for (var i = 1uL; i <= 50uL; i++)
    {
        book.Add(clientId: 1, clientOrderId: i, side: Side.Sell, price: 100 + (long)(i % 10), qty: 10);
        book.Add(clientId: 2, clientOrderId: i, side: Side.Buy, price: 100 + (long)(i % 10), qty: 10);
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var bytesBefore = GC.GetAllocatedBytesForCurrentThread();

    // Act: Process 10,000 orders (sustained bulk Adds, Matches, and Cancels)
    for (var i = 100uL; i < 5_100uL; i++)
    {
        var client1 = (uint)(i % 50) + 1;
        var client2 = (uint)((i + 1) % 50) + 1;

        book.Add(clientId: client1, clientOrderId: i, side: Side.Sell, price: 200 + (long)(i % 20), qty: 5);
        book.Add(clientId: client2, clientOrderId: i + 10_000, side: Side.Buy, price: 200 + (long)(i % 20), qty: 5);
    }

    var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
    var totalAllocatedBytes = bytesAfter - bytesBefore;

    // Assert zero bytes allocated on the managed heap
    totalAllocatedBytes.Should().Be(0, "no bytes should be allocated to the managed heap");
}
```

---

## 4. Multi-Tiered Performance Pipeline Overview

In a complete low-latency pipeline, lightweight unit testing is Tier 1:

```
[ Tier 1: xUnit In-Process ]  ──> Fast check on every build: GC.GetAllocatedBytes == 0
[ Tier 2: BenchmarkDotNet  ]  ──> Micro-benchmarks: Nanosecond throughput & [MemoryDiagnoser]
[ Tier 3: Tail Latency     ]  ──> High-precision p99/p99.99 latency histograms (HdrHistogram)
```

We will cover Tier 2 (BenchmarkDotNet) and Tier 3 (Tail Latency profiling) in dedicated documentation.

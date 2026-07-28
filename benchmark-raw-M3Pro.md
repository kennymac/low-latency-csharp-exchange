# Benchmark Results — Apple M3 Pro

**Environment Information:**
* **Date:** 2026-07-28
* **OS:** macOS 26.5.2 (Darwin 25.5.0)
* **CPU:** Apple M3 Pro (12 Cores: 6 Performance, 6 Efficiency)
* **Runtime:** .NET 10.0.5 (Arm64 RyuJIT AdvSIMD)
* **SDK:** .NET SDK 10.0.201

---

## Summary Table

| Benchmark Suite | Method | Mean Latency | Error | StdDev | Ratio | Allocated |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`SpscRingBufferBenchmark`** | **`SpscRingBuffer_PushAndPop`** (Baseline) | **1.279 ns** | **0.015 ns** | **0.014 ns** | **1.00** | **0 B** |
| | `ConcurrentQueue_PushAndPop` | **2.974 ns** | 0.030 ns | 0.028 ns | 2.33 | 0 B |
| | `SystemThreadingChannel_PushAndPop` | **15.614 ns** | 0.146 ns | 0.129 ns | 12.21 | 0 B |
| **`OrderServerBenchmark`** | **`DecodeAndEnqueueBinaryFrame`** | **2.788 ns** | **0.029 ns** | **0.027 ns** | N/A | **0 B** |
| **`MatchingEngineBenchmark`** | **`MatchOrderPair`** | **22.620 ns** | **0.815 ns** | **2.389 ns** | N/A | **0 B** |

---

## Benchmark Details

### 1. SPSC Ring Buffer (`SpscRingBufferBenchmark`)
* **`SpscRingBuffer_PushAndPop`**: High-performance, cache-aligned single-producer single-consumer lock-free ring buffer.
* **`ConcurrentQueue_PushAndPop`**: .NET standard `System.Collections.Concurrent.ConcurrentQueue<T>`.
* **`SystemThreadingChannel_PushAndPop`**: Bounded `System.Threading.Channels.Channel<T>` configured for single-writer/single-reader.

### 2. Binary Order Server (`OrderServerBenchmark`)
* **`DecodeAndEnqueueBinaryFrame`**: Binary protocol frame deserialization via `MemoryMarshal` zero-copy struct casting and enqueuing onto the ring buffer in **2.79 ns**.

### 3. Matching Engine Core (`MatchingEngineBenchmark`)
* **`MatchOrderPair`**: End-to-end Limit Order Book matching cycle. Enqueues a Sell order (10 @ 100) and immediately matches it against an incoming Buy order (10 @ 100). Full execution recycles order nodes back to `MemPool<Order>` with **0 B managed heap allocations** in **22.62 ns**.

# Benchmark Results — Apple M1

**Environment Information:**
* **Date:** 2026-07-28
* **OS:** macOS 26.5.2 (Darwin 25.5.0)
* **CPU:** Apple M1 (8 Cores: 4 Performance, 4 Efficiency)
* **Runtime:** .NET 10.0.10 (Arm64 RyuJIT AdvSIMD)
* **SDK:** .NET SDK 10.0.302

---

## Summary Table

| Benchmark Suite | Method | Mean Latency | Error | StdDev | Ratio | Allocated |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`SpscRingBufferBenchmark`** | **`SpscRingBuffer_PushAndPop`** (Baseline) | **9.582 ns** | **0.041 ns** | **0.032 ns** | **1.00** | **0 B** |
| | `ConcurrentQueue_PushAndPop` | **11.087 ns** | 0.105 ns | 0.093 ns | 1.16 | 0 B |
| | `SystemThreadingChannel_PushAndPop` | **26.130 ns** | 0.361 ns | 0.337 ns | 2.73 | 0 B |
| **`OrderServerBenchmark`** | **`DecodeAndEnqueueBinaryFrame`** | **10.433 ns** | **0.079 ns** | **0.070 ns** | N/A | **0 B** |
| **`MatchingEngineBenchmark`** | **`MatchOrderPair`** | **35.097 ns** | **0.609 ns** | **0.652 ns** | N/A | **0 B** |

---

## Benchmark Details

### 1. SPSC Ring Buffer (`SpscRingBufferBenchmark`)
* **`SpscRingBuffer_PushAndPop`**: High-performance, cache-aligned single-producer single-consumer lock-free ring buffer.
* **`ConcurrentQueue_PushAndPop`**: .NET standard `System.Collections.Concurrent.ConcurrentQueue<T>`.
* **`SystemThreadingChannel_PushAndPop`**: Bounded `System.Threading.Channels.Channel<T>` configured for single-writer/single-reader.

### 2. Binary Order Server (`OrderServerBenchmark`)
* **`DecodeAndEnqueueBinaryFrame`**: Binary protocol frame deserialization via `MemoryMarshal` zero-copy struct casting and enqueuing onto the ring buffer in **10.43 ns**.

### 3. Matching Engine Core (`MatchingEngineBenchmark`)
* **`MatchOrderPair`**: End-to-end Limit Order Book matching cycle. Enqueues a Sell order (10 @ 100) and immediately matches it against an incoming Buy order (10 @ 100). Full execution recycles order nodes back to `MemPool<Order>` with **0 B managed heap allocations** in **35.10 ns**.

# 10 Hardware Microarchitecture Benchmark Comparison: Intel Xeon W-3245 vs. Apple M3 Pro vs. Apple M1

## Overview

This document presents a side-by-side performance and latency comparison of the zero-allocation C# matching engine scratchpad across three distinct CPU hardware microarchitectures:
1. **Apple M3 Pro** (Arm64 Apple Silicon workstation architecture, 128-byte cache line size, AdvSIMD extensions).
2. **Intel Xeon W-3245 @ 3.20 GHz** (x86_64 server architecture, 64-byte cache line size, AVX2 vector extensions).
3. **Apple M1** (Arm64 Apple Silicon entry workstation architecture, 128-byte cache line size, AdvSIMD extensions).

All benchmarks were executed using **BenchmarkDotNet v0.14.0** with `[MemoryDiagnoser]` tracking managed heap allocations.

---

## Key Microarchitecture Observations

1. **Lock-Free SPSC Ring Buffer (`1.28 ns` vs `2.05 ns` vs `9.58 ns`)**:
   * SPSC push/pop completes in **~5.6 CPU clock cycles** on Apple M3 Pro (**1.61x faster** than Intel Xeon and **7.49x faster** than Apple M1).
   * The 128-byte explicit cache-line padding (`[StructLayout(LayoutKind.Explicit)]`) successfully isolated atomic `_head` and `_tail` sequences across both Apple Silicon (128B lines) and Intel Xeon (64B lines) with **0 Bytes allocated**.

2. **Binary Wire Protocol Decoding & Gateway Enqueue (`2.79 ns` vs `4.36 ns` vs `10.43 ns`)**:
   * Binary protocol frame deserialization via `MemoryMarshal` zero-copy struct casting and enqueuing onto the ring buffer completes in **2.79 nanoseconds** on Apple M3 Pro (**1.56x faster** than Xeon and **3.74x faster** than M1).

3. **Matching Engine Core Loop (`22.62 ns` vs `35.10 ns` vs `45.31 ns`)**:
   * Order placement, price level traversal, FIFO matching, response emission, and object recycling executed in **22.62 nanoseconds** on M3 Pro (**1.55x faster** than M1 and **2.00x faster** than Intel Xeon).
   * **Single-Threaded Throughput:** Scales from 22.07 Million match pairs/sec on Xeon to 28.49 Million match pairs/sec on M1 and **44.21 Million match pairs/sec** (**88.42 Million orders/sec**) on Apple M3 Pro.

---

## Cross-Architecture Benchmark Summary Comparison Tables

### 1. Lock-Free SPSC Ring Buffer Push & Pop (`SpscRingBuffer`)

| CPU Architecture | Architecture / Cores | Mean Latency | Throughput (Ops/sec) | Ratio vs Baseline | Managed Heap Allocations |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Apple M3 Pro** | Arm64 (12 Cores) | **1.279 ns** | **781,860,828 ops/sec** | **1.00 (Fastest)** | **0 B** |
| **Intel Xeon W-3245** | X64 @ 3.2GHz (16 Cores) | **2.054 ns** | **486,854,917 ops/sec** | 1.60x slower | **0 B** |
| **Apple M1** | Arm64 (8 Cores) | **9.582 ns** | **104,362,346 ops/sec** | 7.49x slower | **0 B** |

---

### 2. Binary Gateway Wire Decoding (`OrderServer`)

| CPU Architecture | Architecture / Cores | Mean Latency | Packets Decoded & Queued / sec | Managed Heap Allocations |
| :--- | :--- | :--- | :--- | :--- |
| **Apple M3 Pro** | Arm64 (12 Cores) | **2.788 ns** | **358,679,000 packets/sec** | **0 B** |
| **Intel Xeon W-3245** | X64 @ 3.2GHz (16 Cores) | **4.361 ns** | **229,305,205 packets/sec** | **0 B** |
| **Apple M1** | Arm64 (8 Cores) | **10.430 ns** | **95,877,277 packets/sec** | **0 B** |

---

### 3. Core Matching Engine Pair Execution (`OrderBook`)

| CPU Architecture | Architecture / Cores | Mean Latency | Match Pairs / sec | Orders Processed / sec | Managed Heap Allocations |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Apple M3 Pro** | Arm64 (12 Cores) | **22.62 ns** | **44,208,664 pairs/sec** | **88,417,328 orders/sec** | **0 B** |
| **Apple M1** | Arm64 (8 Cores) | **35.10 ns** | **28,490,028 pairs/sec** | **56,980,056 orders/sec** | **0 B** |
| **Intel Xeon W-3245** | X64 @ 3.2GHz (16 Cores) | **45.31 ns** | **22,070,670 pairs/sec** | **44,141,340 orders/sec** | **0 B** |

---

## Microarchitectural Analysis & Key Takeaways

### 1. Zero-Allocation Lock-Free SPSC Ring Buffer (`1.28 ns` vs `2.05 ns` vs `9.58 ns`)
* The custom `SpscRingBuffer` achieved **1.279 nanoseconds per push/pop cycle** on Apple M3 Pro (compared to **2.054 nanoseconds** on Intel Xeon and **9.582 nanoseconds** on Apple M1).
* On Apple M3 Pro, a full enqueue/dequeue operation completes in approximately **5.6 CPU clock cycles**.
* The 128-byte explicit cache-line padding (`[StructLayout(LayoutKind.Explicit)]`) successfully isolated the `_head` and `_tail` sequences across core clusters on x86_64 (64B cache line) and ARM64 (128B cache line), preventing false sharing invalidation traffic.

### 2. Binary Wire Protocol Decoding & Gateway Enqueue (`2.79 ns` vs `4.36 ns` vs `10.43 ns`)
* Direct binary frame struct casting via `MemoryMarshal.Write`/`Read` into the inbound lock-free queue takes **2.79 nanoseconds** per request on M3 Pro and **4.36 nanoseconds** on Xeon.
* Zero string handles or reference allocations were incurred during binary packet parsing across all architectures.

### 3. Matching Engine Core Loop (`22.62 ns` vs `35.10 ns` vs `45.31 ns`)
* The full order matching pipeline (placing a Sell order, placing a Buy order, traversing price level doubly linked lists, executing FIFO volume matching, and recycling order nodes back to `MemPool<Order>`) completed in **22.62 nanoseconds** on Apple M3 Pro.
* **Throughput:**
  * **Apple M3 Pro:** **44.21 Million match pairs / sec** (~88.42 Million orders/sec).
  * **Apple M1:** **28.49 Million match pairs / sec** (~56.98 Million orders/sec).
  * **Intel Xeon W-3245:** **22.07 Million match pairs / sec** (~44.14 Million orders/sec).

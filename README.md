# Low-Latency Financial Exchange & Engine Scratchpad (.NET 10 / C# 12)

This is an *agent-assisted pairing experiment* - can I use [Google Antigravity 2.0](https://antigravity.google/) to migrate an [existing C++ low-latency codebase](https://github.com/PacktPublishing/Building-Low-Latency-Applications-with-CPP) to modern C#... and hit decent benchmarks with zero allocations? *In just a couple of hours?*

The benchmarked answer, below, is yes!

- Zero-allocation, ultra-low-latency C# financial exchange engine
- The pair programming work took around 3 hours, proceeding step by step in a TDD test-and-review cycle, digging into concepts as required, as well as producing covering documentation for each phase - not letting the agent spool out the entire solution in just a few minutes
- Recommended reading for .net and CPU and memory architecture concepts is [Pro .NET Memory Management: For Better Code, Performance, and Scalability](https://www.amazon.co.uk/Pro-NET-Memory-Management-Performance/dp/B0D3PNGKZR)

The baseline C++ codebase used for the experiment was the exchange architecture outlined in Sourav Ghosh's book [Building Low Latency Applications with C++: Develop a complete low latency trading ecosystem from scratch using modern C++](https://www.amazon.co.uk/Building-Low-Latency-Applications-ecosystem/dp/1837639353).

## Approach

Given that dotnet core CLR lives within a garbage collected runtime, I followed the well-known 2011 [LMAX Disruptor](https://lmax-exchange.github.io/disruptor/) approach, which achieved its speeds by **avoiding GC entirely on the hot path**.  In modern C#, by using `Unsafe`, pre-allocated byte buffers, and zero-allocation object pools, we can achieve similarly impressive results.  

Thus, we repeat Java’s early success around **zero-allocation idioms**, to  highlight why C# can now do exactly the same thing natively with `Span<T>` and `ref struct`.

---

## BenchmarkDotNet Performance

### Key Microarchitecture Observations

1. **Lock-Free SPSC Ring Buffer (`1.28 ns` on M3 Pro vs `2.05 ns` on Xeon vs `9.58 ns` on M1)**:
   * SPSC push/pop completes in **~5.6 CPU clock cycles** on Apple M3 Pro (**1.61x faster** than Intel Xeon and **7.49x faster** than M1).
   * 128-byte explicit cache-line padding (`[StructLayout(LayoutKind.Explicit)]`) isolated atomic `_head` and `_tail` sequences across Apple Silicon M3 Pro/M1 (128B lines) and Intel Xeon (64B lines) with **0 Bytes allocated**.

2. **Binary Wire Protocol Decoding & Gateway Enqueue (`2.79 ns` vs `4.36 ns` vs `10.43 ns`)**:
   * Binary protocol frame deserialization via `MemoryMarshal` zero-copy struct casting and enqueuing onto the ring buffer completes in **2.79 nanoseconds** on Apple M3 Pro (**1.56x faster** than Xeon and **3.74x faster** than M1).

3. **Matching Engine Core Loop (`22.62 ns` vs `35.10 ns` vs `45.31 ns`)**:
   * Order placement, price level traversal, FIFO matching, response emission, and object recycling executed in **22.62 nanoseconds** on M3 Pro (**1.55x faster** than M1 and **2.00x faster** than Intel Xeon).
   * **Single-Threaded Throughput:** Scales from 22.07 Million match pairs/sec on Xeon to **44.21 Million match pairs/sec** (**88.42 Million orders/sec**) on Apple M3 Pro.

---

### Microarchitecture Benchmark Comparison: Intel Xeon W-3245 vs. Apple M3 Pro vs. Apple M1

#### 1. Lock-Free SPSC Ring Buffer Push & Pop (`SpscRingBuffer`)

| CPU Architecture | Architecture / Cores | Mean Latency | Throughput (Ops/sec) | Ratio vs Baseline | Heap Allocated |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Apple M3 Pro** | Arm64 (12 Cores) | **1.279 ns** | **781,860,828 ops/sec** | **1.00 (Fastest)** | **0 B** |
| **Intel Xeon W-3245** | X64 @ 3.2GHz (16 Cores) | **2.054 ns** | **486,854,917 ops/sec** | 1.60x slower | **0 B** |
| **Apple M1** | Arm64 (8 Cores) | **9.582 ns** | **104,362,346 ops/sec** | 7.49x slower | **0 B** |

##### Queue Standard Library Comparison (On M3 Pro):
* **`SpscRingBuffer<T>`:** **1.279 ns** (781.8M ops/sec) — **0 B Heap Allocated**
* **`ConcurrentQueue<T>`:** **2.974 ns** (336.2M ops/sec) — **0 B Heap Allocated** (2.33\times slower)
* **`System.Threading.Channels`:** **15.614 ns** (64.0M ops/sec) — **0 B Heap Allocated** (12.21\times slower)

---

#### 2. Binary Gateway Wire Decoding (`OrderServer`)

| CPU Architecture | Architecture / Cores | Mean Latency | Packets Decoded & Queued / sec | Heap Allocated |
| :--- | :--- | :--- | :--- | :--- |
| **Apple M3 Pro** | Arm64 (12 Cores) | **2.788 ns** | **358,679,000 packets/sec** | **0 B** |
| **Intel Xeon W-3245** | X64 @ 3.2GHz (16 Cores) | **4.361 ns** | **229,305,205 packets/sec** | **0 B** |
| **Apple M1** | Arm64 (8 Cores) | **10.430 ns** | **95,877,277 packets/sec** | **0 B** |

---

#### 3. Core Matching Engine Pair Execution (`OrderBook`)

| CPU Architecture | Architecture / Cores | Mean Latency | Match Pairs / sec | Orders Processed / sec | Heap Allocated |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Apple M3 Pro** | Arm64 (12 Cores) | **22.62 ns** | **44,208,664 pairs/sec** | **88,417,328 orders/sec** | **0 B** |
| **Apple M1** | Arm64 (8 Cores) | **35.10 ns** | **28,490,028 pairs/sec** | **56,980,056 orders/sec** | **0 B** |
| **Intel Xeon W-3245** | X64 @ 3.2GHz (16 Cores) | **45.31 ns** | **22,070,670 pairs/sec** | **44,141,340 orders/sec** | **0 B** |

---

## CPU Hardware Clock Cycle & Performance Breakdown

> [!IMPORTANT]
> **Hardware Execution Breakdown across Architectures:**
> 
> * **`SpscRingBuffer` Push & Pop:**
>   * **Apple M3 Pro:** **1.279 ns** (~5.6 CPU clock cycles total)
>   * **Intel Xeon W-3245:** **2.054 ns** (~6.5 CPU clock cycles total)
>   * **Apple M1:** **9.582 ns** (~30 CPU clock cycles total)
> * **`MatchOrderPair` (Order Matching Engine):**
>   * **Apple M3 Pro:** **22.62 ns** (~90 CPU clock cycles total — **44.2 Million match pairs / 88.4M orders per second**)
>   * **Apple M1:** **35.10 ns** (~112 CPU clock cycles total — **28.5 Million match pairs / 57.0M orders per second**)
>   * **Intel Xeon W-3245:** **45.31 ns** (~145 CPU clock cycles total — **22.1 Million match pairs / 44.1M orders per second**)
> * **Zero Managed Heap Allocation:** **0 Bytes allocated** across all 3 architectures, guaranteeing 100% immunity from Garbage Collection (GC) pauses.

---

## Key Mechanical Sympathy Highlights
* **Zero Heap Allocations:** **0 B** allocated per operation across matching, lock-free queueing, async logging, and binary wire decoding.
* **Bare-Metal Speed:** `SpscRingBuffer<T>` executes **push and pop in 1.279 nanoseconds** on M3 Pro (**781M ops/sec**).
* **128-Byte Cache Line Padding:** Eliminates CPU false sharing on Apple Silicon (M1/M4 ARM64) and Intel Xeon (64B) cores (`PaddedSequence`).
* **Power-of-Two Bitwise Masking:** Replaces 10–30 cycle hardware division instructions with 1-cycle bitwise `AND` masks (`sequence & mask`).
* **LMAX Disruptor Batch Dequeue:** `TryDequeueBatch` drains published sequences in single-pass batch loops.

---

## How to Build & Run Benchmarks

### 1. Run Unit Tests
```bash
dotnet test
```

### 2. Run BenchmarkDotNet Suite (Direct Execution)
Run all micro-benchmarks in Release mode directly from your terminal:
```bash
dotnet run -c Release --project LowLatency.ScratchPad.Benchmarks --filter "*"
```

### 3. Publish & Run Standalone Benchmark Host Executable
Alternatively, publish and execute the self-contained Release binary:
```bash
# Publish self-contained release executable (Apple Silicon / macOS ARM64)
dotnet publish LowLatency.ScratchPad.Benchmarks/LowLatency.ScratchPad.Benchmarks.csproj -c Release -r osx-arm64 --self-contained

# Execute published benchmark host binary
./LowLatency.ScratchPad.Benchmarks/bin/Release/net10.0/osx-arm64/publish/LowLatency.ScratchPad.Benchmarks --filter "*"
```
> **Important Note on Native AOT:** Core engine/library projects (`LowLatency.ScratchPad.Engine.csproj`) **CAN and SHOULD** use `<PublishAot>true</PublishAot>` to guarantee zero-pause, bare-metal native binaries. However, BenchmarkDotNet host runner projects (`LowLatency.ScratchPad.Benchmarks.csproj`) **MUST NOT** enable `<PublishAot>true</PublishAot>` because BenchmarkDotNet's host CLI parser (`CommandLineParser`) relies on reflection metadata that Native AOT IL trimming strips out.

---

## Core System Architecture

```text
 [ Trading Client ]
         │ (Binary Wire Packet - 32B)
         ▼
 [ OrderServer Gateway ]  ──>  [ MemoryMarshal.Read ~2ns ]
         │
         ▼ (SpscRingBuffer<ClientRequest>)
 [ Core MatchingEngine ]  ──>  [ MemPool<Order> + Flat Array Indexing ]
         │
         ├──> (SpscRingBuffer<ClientResponse>) ──> OrderServer (TCP Output)
         ├──> (SpscRingBuffer<MarketUpdate>)   ──> Market Data Publisher (Multicast)
         └──> (SpscRingBuffer<LogEntry>)       ──> LowLatencyLogger (Disk Journaler)
```

---

## Running Unit Tests & Benchmarks

```bash
# Run all 25 unit tests (0 bytes allocated verification)
dotnet test LowLatency.ScratchPad.MatchingEngine.UnitTests/LowLatency.ScratchPad.MatchingEngine.UnitTests.csproj

# Run BenchmarkDotNet profiling suite
dotnet run -c Release --project LowLatency.ScratchPad.Benchmarks/LowLatency.ScratchPad.Benchmarks.csproj
```

---

## Documentation Table of Contents

All guides are registered inside `LowLatency.ScratchPad.sln` under the `Documentation` folder:

* [00 How To Feed Signals Into the App or Probe a running console app.md](Documentation/00%20How%20To%20Feed%20Signals%20Into%20the%20App%20or%20Probe%20a%20running%20console%20app.md)
* [01 Memory Coherence Volatile and Lock-Free Ring Buffer Hardware Architecture.md](Documentation/01%20Memory%20Coherence%20Volatile%20and%20Lock-Free%20Ring%20Buffer%20Hardware%20Architecture.md)
* [02 How To Create Low Latency Unit Tests Without Heavyweight Test Harnesses.md](Documentation/02%20How%20To%20Create%20Low%20Latency%20Unit%20Tests%20Without%20Heavyweight%20Test%20Harnesses.md)
* [03 LMAX Disruptor Architecture and Mechanical Sympathy in CSharp.md](Documentation/03%20LMAX%20Disruptor%20Architecture%20and%20Mechanical%20Sympathy%20in%20CSharp.md)
* [04 Power of Two Bitwise Masking vs Modulo Division.md](Documentation/04%20Power%20of%20Two%20Bitwise%20Masking%20vs%20Modulo%20Division.md)
* [05 Cache Line Padding and StructLayout Memory Alignment in CSharp.md](Documentation/05%20Cache%20Line%20Padding%20and%20StructLayout%20Memory%20Alignment%20in%20CSharp.md)
* [06 Event Emission and Outbound Ring Buffer Architecture in Low Latency Engines.md](Documentation/06%20Event%20Emission%20and%20Outbound%20Ring%20Buffer%20Architecture%20in%20Low%20Latency%20Engines.md)
* [07 Binary Network Protocols and SBE vs FIX in High Frequency Trading.md](Documentation/07%20Binary%20Network%20Protocols%20and%20SBE%20vs%20FIX%20in%20High%20Frequency%20Trading.md)
* [08 BenchmarkDotNet Profiling Harness and Allocation Verification in CSharp.md](Documentation/08%20BenchmarkDotNet%20Profiling%20Harness%20and%20Allocation%20Verification%20in%20CSharp.md)
* [09 Native AOT Compilation and Bare Metal CSharp Performance.md](Documentation/09%20Native%20AOT%20Compilation%20and%20Bare%20Metal%20CSharp%20Performance.md)
* [10 Hardware Microarchitecture Benchmark Comparison Intel Xeon vs Apple M3 Pro vs Apple M1.md](Documentation/10%20Hardware%20Microarchitecture%20Benchmark%20Comparison%20Intel%20Xeon%20vs%20Apple%20M3%20Pro%20vs%20Apple%20M1.md)

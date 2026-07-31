# Low-Latency Financial Exchange Engine & Microarchitecture Benchmark (.NET 10 / C# 12)

This is an *agent-assisted pairing experiment* - can I use [Google Antigravity 2.0](https://antigravity.google/) to migrate an [existing C++ low-latency codebase](https://github.com/PacktPublishing/Building-Low-Latency-Applications-with-CPP) to modern C#... and hit decent benchmarks with zero allocations? *In just a couple of hours?*

The benchmarked answer, below, is yes!

- Zero-allocation, ultra-low-latency C# financial exchange engine
- The pair programming work took ~4.5 hours, proceeding step by step in a TDD test-and-review cycle, digging into concepts as required, as well as producing covering documentation for each phase - not letting the agent spool out the entire solution in just a few minutes
- All .NET Core 10 running on macOS using [Jetbrains' Rider](https://www.jetbrains.com/rider/) IDE
- The baseline C++ codebase used for the experiment was the exchange architecture outlined in Sourav Ghosh's book ***Building Low Latency Applications with C++: Develop a complete low latency trading ecosystem from scratch using modern C++*** (see [Recommended Reading](#recommended-reading), below).

## Approach

Given that dotnet core lives within a garbage collected runtime, I followed the well-known 2011 [LMAX Disruptor](https://lmax-exchange.github.io/disruptor/) approach, which achieved its speeds by **avoiding GC entirely on the hot path**.  In modern C#, by using `Unsafe`, pre-allocated byte buffers, and zero-allocation object pools, we can achieve similarly impressive results.  

Thus, we repeat Java’s early success around **zero-allocation idioms**, to  highlight why C# can now do exactly the same thing natively with `Span<T>` and `ref struct`.

---

### Agent-Pairing Workflow & Steering Rules

The engine was constructed over a ~4.5-hour pairing session with an AI agent (*Google Antigravity 2.0*), taking the architecture piece by piece in an interactive TDD rhythm:
1. **Interactive TDD Review Cycle:** The agent was instructed to write unit tests first, pause, and wait for code review before implementing the matching logic or lock-free queue.
2. **Microarchitecture Exploration:** This step-by-step pace created natural checkpoints to ask questions about CPU cycle counts, cache line sizes, and generate [in-depth documentation](#documentation) for key concepts (cache alignment, bitwise masking, Native AOT).
3. **Explicit Agent Rules:** The agent was governed by strict steering rules defined in [AGENTS.md](AGENTS.md) (enforcing 0-byte heap allocations, DAMP testing, and artifact preservation).

💬 For a fuller account of the experiment, join the conversation in [GitHub Discussions](https://github.com/kennymac/low-latency-csharp-exchange/discussions).

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

### Empirical Comparison: Zero-Allocation C# vs. Idiomatic C# (Apple M3 Pro)

To evaluate the exact performance gain of custom low-latency memory design versus standard C# programming, we implemented an **`IdiomaticEngine`** (`LowLatency.ScratchPad.IdiomaticEngine`) using conventional object-oriented patterns (`class Order`, `new Order()`, `SortedDictionary<long, LinkedList<Order>>`, `ConcurrentQueue<T>`) executing identical matching logic.

#### BenchmarkDotNet Results (Apple M3 Pro Host):

| Matching Engine Architecture | Mean Latency | Median Latency | Throughput (Pairs / sec) | Managed Heap Allocations | GC Gen0 Collections / 1k ops | Latency Ratio |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Zero-Allocation Engine** *(Current)* | **25.16 ns** | **23.44 ns** | **39,745,000 pairs/sec** | **0 B** | **0.0000** | **1.00x (Baseline)** |
| **Idiomatic C# Engine** *(Standard OOP)* | **53.51 ns** | **53.42 ns** | **18,688,000 pairs/sec** | **328 B / pair** | **0.0392** | **2.22x slower** |

* **Key Takeaway:** The zero-allocation architecture is **2.22x faster** in mean latency and eliminates **328 Bytes of heap allocations per matched pair** (guaranteeing 100% immunity from GC pauses under load).

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


### 4. Stochastic Mutation Testing (`watchfails` Branch Spike)
To inspect or run a stochastic mutation test pass (fault injection audit):
1. Switch to the dedicated `watchfails` demonstration branch:
   ```bash
   git checkout watchfails
   ```
2. The `watchfails` branch maintains an explicit step-by-step commit history demonstrating 8 deliberate production mutations (off-by-one bounds, mask errors, memory barrier bypasses, pool node leaks) alongside their corresponding test failures and green restorations.
3. Review [WatchTestFailures.md](file:///Users/kenmccormack/low-latency-scratchpad/WatchTestFailures.md) on the `watchfails` branch for the full fault injection matrix and remediation details.
4. To request a new mutation testing audit during development, simply instruct the agent: *"Run a stochastic mutation test pass."*

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

## Documentation

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


--- 

## Recommended Reading

- For patterns and trading exchange architecture components, see ***Building Low Latency Applications with C++: Develop a complete low latency trading ecosystem from scratch using modern C++*** [Packt](https://www.packtpub.com/en-us/product/building-low-latency-applications-with-c-9781837639359) | [GitHub](https://github.com/PacktPublishing/Building-Low-Latency-Applications-with-CPP) | [Amazon](https://link.amazon/B00v7H7GK)

- For .NET low level optimisation and memory management fundamentals, see ***Pro .NET Memory Management: For Better Code, Performance, and Scalability*** [Apress](https://link.springer.com/book/10.1007/978-1-4842-4027-4) | [GitHub](https://github.com/Apress/pro-.net-memory) | [Amazon](https://amzn.to/4yNR3hT)

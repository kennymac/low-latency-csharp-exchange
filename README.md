# Low-Latency Financial Exchange & Engine Scratchpad (.NET 10 / C# 12)

A zero-allocation, ultra-low-latency C# financial exchange engine scratchpad modeled after Sourav Ghosh's C++ exchange architecture and Martin Thompson's LMAX Disruptor design.

---

## Official BenchmarkDotNet Performance & Hardware Comparison

### Key Microarchitecture Observations

1. **Lock-Free SPSC Ring Buffer (`1.28 ns` vs `2.05 ns`)**:
   * SPSC push/pop completes in **~5.6 CPU clock cycles** on Apple M3 Pro (**1.61x faster** than Intel Xeon).
   * 128-byte explicit cache-line padding (`[StructLayout(LayoutKind.Explicit)]`) isolated atomic `_head` and `_tail` sequences across both Apple Silicon M3 Pro (128B lines) and Intel Xeon (64B lines) with **0 Bytes allocated**.

2. **Binary Wire Protocol Decoding & Gateway Enqueue (`2.79 ns` vs `4.36 ns`)**:
   * Binary protocol frame deserialization via `MemoryMarshal` zero-copy struct casting and enqueuing onto the ring buffer completes in **2.79 nanoseconds** on Apple M3 Pro (**1.56x faster**).

3. **Matching Engine Core Loop (`22.62 ns` vs `40.68 ns`)**:
   * Order placement, price level traversal, FIFO matching, response emission, and object recycling executed in **22.62 nanoseconds** on M3 Pro (**1.80x faster** than Intel Xeon).
   * **Single-Threaded Throughput:** Increased from 24.58 Million match pairs/sec on Xeon to **44.21 Million match pairs/sec** (**88.42 Million orders/sec**) on Apple M3 Pro.

---

### Microarchitecture Benchmark Comparison: Intel Xeon W-3245 vs. Apple M3 Pro

| Benchmark Suite | Benchmark Method | Intel Xeon W-3245 (3.20 GHz x86_64) | Apple M3 Pro (Arm64) | Latency Reduction | Throughput (Apple M3 Pro) | Managed Heap Allocations |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`SpscRingBufferBenchmark`** | **`SpscRingBuffer_PushAndPop`** (Baseline) | **2.054 ns** | **1.279 ns** | **-37.7%** | **781,860,828 ops/sec** | **0 B** |
| | `ConcurrentQueue_PushAndPop` | 12.252 ns | 2.974 ns | -75.7% | 336,247,478 ops/sec | 0 B |
| | `SystemThreadingChannel_PushAndPop` | 45.225 ns | 15.614 ns | -65.5% | 64,045,087 ops/sec | 0 B |
| **`OrderServerBenchmark`** | **`DecodeAndEnqueueBinaryFrame` (Gateway)** | **4.361 ns** | **2.788 ns** | **-36.1%** | **358,679,997 ops/sec** | **0 B** |
| **`MatchingEngineBenchmark`** | **`MatchOrderPair` (Matching Engine)** | **40.680 ns** | **22.620 ns** | **-44.4%** | **44,208,665 pairs/sec** <br> *(88,417,330 orders/sec)* | **0 B** |

---

## CPU Hardware Clock Cycle & Performance Breakdown

> [!IMPORTANT]
> **Hardware Execution Breakdown:**
> 
> * **`SpscRingBuffer` Push & Pop (`1.279 ns` on M3 Pro / `2.054 ns` on Xeon):** Takes **~5.6 CPU clock cycles total** for a full lock-free enqueue + dequeue cycle!
> * **`MatchOrderPair` (`22.62 ns` on M3 Pro / `40.68 ns` on Xeon):** Takes **~90 CPU clock cycles total** to receive two orders, traverse price levels, execute FIFO matching, emit client responses, and recycle memory pool nodes with zero GC pressure!
> * **Throughput:** Apple M3 Pro achieves **44.2 Million matched pairs / second** (**88.4 Million orders/sec**) single-threaded!

---

## Key Mechanical Sympathy Highlights
* **Zero Heap Allocations:** **0 B** allocated per operation across matching, lock-free queueing, async logging, and binary wire decoding.
* **Bare-Metal Speed:** `SpscRingBuffer<T>` executes **push and pop in 2.054 nanoseconds** (**6x faster** than `ConcurrentQueue`, **22x faster** than `System.Threading.Channels`).
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
> **Terminal Environment Note:** In standard macOS/Linux user terminals, your shell automatically sets `$HOME`. No custom environment variables are required.

### 3. Publish & Run Standalone Benchmark Host Executable
Alternatively, publish and execute the self-contained Release binary:
```bash
# Publish self-contained release executable (Apple Silicon / macOS ARM64)
dotnet publish LowLatency.ScratchPad.Benchmarks/LowLatency.ScratchPad.Benchmarks.csproj -c Release -r osx-arm64 --self-contained

# Execute published benchmark host binary
./LowLatency.ScratchPad.Benchmarks/bin/Release/net10.0/osx-arm64/publish/LowLatency.ScratchPad.Benchmarks --filter "*"
```
> **Important Note on Native AOT:** Core engine/library projects (`LowLatency.ScratchPad.Engine.csproj`) **CAN and SHOULD** use `<PublishAot>true</PublishAot>` to guarantee zero-pause, bare-metal native binaries. However, BenchmarkDotNet host runner projects (`LowLatency.ScratchPad.Benchmarks.csproj`) **MUST NOT** enable `<PublishAot>true</PublishAot>` because BenchmarkDotNet's host CLI parser (`CommandLineParser`) relies on reflection metadata that Native AOT IL trimming strips out.

### 4. Build Core Engine with Native AOT
To publish the core engine as a zero-pause, bare-metal Native AOT binary:
```bash
# macOS ARM64 (Apple Silicon M1/M3/M4)
dotnet publish LowLatency.ScratchPad.Engine/LowLatency.ScratchPad.Engine.csproj -c Release -r osx-arm64

# Linux x64 (Production HFT Server Target)
dotnet publish LowLatency.ScratchPad.Engine/LowLatency.ScratchPad.Engine.csproj -c Release -r linux-x64
```

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

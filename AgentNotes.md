# Agent Notes & Handover Log (Agent Scratchpad)

> **Purpose:** This file acts as the inter-session memory log between AntiGravity agent instances. Update this file whenever making architectural trade-offs, skipping steps for later, or making key low-latency design decisions.

---

## Current Workspace Context
* **Scratchpad Location:** `~/Code/low-latency-scratchpad`
* **Reference C++ Codebase:** `~/Code/books-llappswithcpp` (Sourav Ghosh's C++ book with Mac build fixes).
* **Target Hardware Context:**
  * **Current Machine:** Intel Xeon Mac (x86_64, 64-byte cache line size, restricted macOS thread affinity).
  * **Alternative Workstations:** Apple M1 (ARM64, 128-byte cache line size) & M4.

---

## Architectural & Design Log

### 1. Cache Alignment Strategy
* On x86 Xeon, 64-byte padding prevents false sharing. On M1/M4, 128-byte padding is required.
* **Decision:** Parameterize struct padding constants or default to 128 bytes to ensure cross-architecture cache isolation (128 bytes safely covers both 64B and 128B cache lines).

### 2. Lock-Free Queue vs. LMAX Disruptor Pattern
* Ghosh's book implements custom SPSC/MPMC lock-free queues in C++.
* **Decision:** We will benchmark Ghosh's lock-free ring buffer against an LMAX Disruptor-style ring buffer in C# to evaluate latency variance and throughput under high tick rates.

### 3. Order Representation & Memory Layout
* Need fixed-width zero-allocation structs for `Order`, `Trade`, and `MarketUpdate`.
* Avoid string handles for Symbols or Client IDs. Use `long` / `ulong` bitpacked IDs or fixed `byte` arrays (`ReadOnlySpan<byte>`).

### 4. Native AOT & BenchmarkDotNet Host Runner Pitfall
* **Issue:** Setting `<PublishAot>true</PublishAot>` inside `LowLatency.ScratchPad.Benchmarks.csproj` breaks BenchmarkDotNet at runtime with `InvalidOperationException: Type BenchmarkDotNet.ConsoleArguments.CommandLineOptions appears to be immutable, but no constructor found to accept values`.
* **Root Cause:** Native AOT IL trimming strips reflection metadata required by `CommandLineParser` inside the BDN host orchestrator process.
* **Rule:** Keep `<PublishAot>true</PublishAot>` enabled on core engine/library projects (`LowLatency.ScratchPad.Engine.csproj`). Do **NOT** place `<PublishAot>true</PublishAot>` on BDN runner `.csproj` files. To benchmark Native AOT execution with BDN, keep the runner `.csproj` JIT-enabled and annotate benchmark classes with `[SimpleJob(RuntimeMoniker.NativeAot100)]`.

---

## Ongoing Task Backlog & Technical Debt
- [ ] Inspect existing `LowLatency.ScratchPad.Engine` & `Profiler` projects in `~/Code/low-latency-scratchpad`.
- [ ] Implement cache-aligned lock-free SPSC RingBuffer in C#.
- [ ] Add `BenchmarkDotNet` harness with `[MemoryDiagnoser]` to track allocation & throughput.
- [ ] Verify Native AOT build (`<PublishAot>true</PublishAot>`).

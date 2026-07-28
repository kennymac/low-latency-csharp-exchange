# AntiGravity Project Rules & Low-Latency Guidelines

## 1. Core Mission & Repository Architecture
* **Scratchpad Workspace:** `~/Code/low-latency-scratchpad` is an experimental, iterative environment. Agentic notes, scratch code, and exploratory benchmarks belong here.
* **Public Demo Repo Target:** Clean, production-grade components will eventually be extracted from this scratchpad into a public GitHub repository.
* **Documentation & Solution Tracking:** Store markdown articles sequentially in `Documentation/` (e.g. `00 ...`, `01 ...`). Always register newly created markdown files inside the `Documentation` SolutionItems section of `LowLatency.ScratchPad.sln`.

---

## 2. Low-Latency C# Constraints (Hot Path Rules)

### Zero Heap Allocation Policy
* **No `new` on Hot Paths:** No reference object instantiations, string concatenations, boxing operations, or closure captures in hot execution loops.
* **Memory Management:** Use `Span<T>`, `ReadOnlySpan<T>`, `ref struct`, `stackalloc`, `ArrayPool<T>`, and `MemoryPool<T>`.
* **Devirtualization:** Avoid interface pointers on hot paths. Use generic type parameters constrained by structs (`where T : struct, IOrderProcessor`) to enable full JIT devirtualization and inlining.

### Cache Alignment & False Sharing
* **Cache Line Padding:** 
  * Intel x86_64 Xeon: 64-byte cache lines.
  * Apple Silicon (M1/M4 ARM64): 128-byte cache lines.
* **Explicit Layout:** Use `[StructLayout(LayoutKind.Explicit)]` and `[FieldOffset(...)]` for critical atomic counters (producer/consumer indices) to prevent false sharing across core clusters.

### Synchronization & Concurrency
* **Lock-Free Primitives:** Favor lock-free ring buffers (SPSC / MPMC), `Volatile.Read` / `Volatile.Write`, `Interlocked`, and atomic state transitions over heavy OS mutexes/locks.
* **macOS Affinity Awareness:** POSIX thread affinity is restricted on macOS. Use spin-wait strategies (`Thread.SpinWait()`, `SpinWait.SpinOnce()`) and explicit core yielding rather than relying on native thread pinning.

### Native AOT & Compiler Options
* **Native AOT Ready:** Ensure core engine code builds cleanly under `<PublishAot>true</PublishAot>` with zero reflection or dynamic code generation errors.
* **BenchmarkDotNet & Native AOT Rule:** Do **NOT** set `<PublishAot>true</PublishAot>` inside BenchmarkDotNet runner `.csproj` files (e.g. `LowLatency.ScratchPad.Benchmarks.csproj`). Publishing the BDN host orchestrator as Native AOT enables IL trimming, which strips reflection metadata required by `CommandLineParser` (`BenchmarkDotNet.ConsoleArguments.CommandLineOptions`) and throws `InvalidOperationException` on CLI argument parsing. To benchmark Native AOT execution, keep the host `.csproj` JIT-enabled and annotate benchmark classes with `[SimpleJob(RuntimeMoniker.NativeAot100)]` or `[SimpleJob(RuntimeMoniker.NativeAot90)]`.
* **Inlining:** Annotate critical micro-functions with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.

---

## 3. Testing & Benchmarking Workflow
* **TDD First:** Write unit tests for correctness before optimizing for speed.
* **Interactive Pair Programming Review:** When reviewing test suites or multi-stage implementations with the user, present tests/code step-by-step in small batches (2–3 tests at a time) and pause for review before proceeding.
* **BenchmarkDotNet:** Every data structure / component must have a corresponding BenchmarkDotNet harness annotated with `[MemoryDiagnoser]` to enforce **0 Bytes allocated per operation**.

---

## 4. C# Code Style & Formatting Directives

### Unit Test Naming & Structure
* **Test Class Naming:** Test class names must be singular ending in "Test" (e.g. `OrderBookTest`, not `OrderBookTests`).
* **Test Method Naming Pattern:** Use Given/When/Then naming format:
  `MethodNameOrWhen_GivenTheStateBeforeHand_ThenTheExpectedStateOrOutcome()`
* **Block Comments:** Structure test bodies with `// Arrange`, `// Act`, `// Assert` block headers.
* **Allocation Assertion Grammar:** In zero-allocation tests, use exact comment grammar:
  `// Assert zero bytes allocated on the managed heap`
* **DAMP Allocation Pattern:** Keep zero-allocation test setup self-contained and explicit (DAMP - Descriptive And Meaningful Phrases) rather than extracting helper methods. Seeing the explicit GC collection, warmup, `bytesBefore`, and `bytesAfter` sequence makes the measurement transparent and prevents hidden delegate allocation artifacts.
* **Clean Null Assertions:** Avoid redundant `!` null-suppression operators after `.Should().NotBeNull()`. `book.AsksByPrice.Price` is preferred over `book.AsksByPrice!.Price`.

### Code Formatting & Type Usage
* **`var` Keyword:** Use `var` for local variable declarations (e.g. `var bytesBefore = GC.GetAllocatedBytesForCurrentThread();`) unless explicit type visibility is helpful.
* **Collection Expressions:** Use C# 12 collection expressions (`[]`) instead of `new List<T>()` or `new()` (e.g. `private readonly List<ClientResponse> _responses = [];`).
* **Control Flow Braces:** Always use explicit braces `{}` for all `if`, `else`, `for`, and `while` blocks. Avoid unbraced single-line statements.
* **Parameter Folding:** Fold multi-line parameter invocations with 1 parameter per line.
* **Named Arguments:** Use explicit named arguments (`clientId: clientId`, `tickerId: tickerId`) when passing multiple parameters of similar primitive types (`uint`, `ulong`) to prevent subtle ordering bugs.
* **Naming Conventions:**
  * All method and primary constructor parameters (including `record struct` parameters) must use `camelCase`.
  * Private instance fields must use `_camelCase`.
* **Domain Model Organization:** Organize domain types in a `Model/` directory with 1 file per type (`Side.cs`, `ClientResponse.cs`, etc.). Avoid grouping unrelated types into a monolithic `Types.cs` or `Enums.cs` file.
* **File Encoding & Line Endings:**
  * **Charset:** UTF-8 without BOM (`utf-8`).
  * **Line Endings:** Unix-style LF (`\n`).
  * **Final Newline:** Always insert a final trailing newline at the end of every file.
* **Clean Imports:** Keep imports clean; remove all unused `using` directives.

# AI Agent Steering Rules & Engineering Directives (`AGENTS.md`)

This document outlines the strict engineering directives and behavioral rules used by AI agentic tooling (**Google AntiGravity 2.0**) during the construction of this low-latency C# exchange engine.

---

## 1. Hot-Path Zero Managed Heap Allocation Rules
* **Strict Zero Allocations:** All code on the hot execution path (order matching, lock-free queueing, wire protocol decoding, async logging) MUST allocate **0 Bytes** on the managed heap.
* **Low-Level Primitives:** Use `Span<T>`, `ReadOnlySpan<T>`, `ref struct`, `stackalloc`, `Unsafe`, and `MemoryMarshal` zero-copy struct casting.
* **Devirtualization:** Avoid interface dispatch on hot paths. Use generic type parameters constrained by structs to enable full JIT devirtualization and inlining.

---

## 2. Interactive TDD & Pair Programming Rhythm
* **Incremental Step-by-Step Rhythm:** Work in small, isolated TDD steps (2–3 tests at a time). Never attempt multi-file boilerplate generation without user review.
* **Pause for Code Review:** Present code and tests step-by-step, explain non-obvious microarchitecture rationale, and wait for human review before proceeding.
* **DAMP Unit Testing:** Follow DAMP (Descriptive And Meaningful Phrases) over DRY in unit tests. Keep allocation measurement sequences (`GC.GetAllocatedBytesForCurrentThread()`) transparent and self-contained.
* **Boundary Condition Testing Focus:** Redundant variants of happy-path unit tests are generally discouraged. Test suites must prioritize covering the edges of the state space—including upper, lower, zero, empty/zero-length, capacity limit, and overflow bounds.
* **Zero-Trust Source Translation:** Never assume reference code or ported algorithms (even from published textbooks, academic repos, or sample spikes) are defect-free or production-ready. When translating logic across languages (e.g., C++ to C#), explicitly audit memory indexing, modulo arithmetic, capacity limits, and unstated domain assumptions before declaring port fidelity.
* **State-Space Property & Stress Fuzzing:** New stateful components (order books, ring buffers, memory pools) must be paired with property-based tests (FsCheck) or randomized stress sequences using arbitrary 64-bit domain inputs (`long.MinValue/MaxValue`, wide price spreads, non-sequential IDs) to detect structural collisions before performance benchmarking.

---

## 3. Microarchitecture & Hardware Isolation
* **Cache Line Padding:** Enforce 128-byte explicit cache-line padding (`[StructLayout(LayoutKind.Explicit)]`) to eliminate CPU false sharing across Apple Silicon (128B) and Intel Xeon (64B + L2 adjacent line prefetcher) cores.
* **Power-of-Two Bitwise Masking:** Replace division/modulo instructions (`sequence % capacity`) with single-cycle bitwise `AND` masks (`sequence & mask`).
* **Modulo & Array Indexing Safety:** Direct modulo arithmetic (`key % N`) on domain values (such as prices, timestamps, or client IDs) is strictly forbidden for array slot lookup unless key inputs are provably bounded within $0 \dots N-1$. All hash/slot mappings must use validated bitwise power-of-two masking (`key & mask`) with explicit collision resolution (e.g., open addressing / linear probing).


---

## 4. Benchmark Preservation Policy
* **Artifact Preservation:** NEVER overwrite existing baseline hardware benchmark reports in `BenchmarkDotNet.Artifacts/`. Isolate new benchmark runs into dedicated hardware/comparison subdirectories.

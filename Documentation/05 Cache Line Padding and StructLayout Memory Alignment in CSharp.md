# Cache Line Padding and StructLayout Memory Alignment in C#

This document details why and how we use `[StructLayout(LayoutKind.Explicit, Size = 256)]` and `[FieldOffset(...)]` to eliminate **False Sharing** across CPU cores, how the CLR maps managed structs to CPU cache hardware, and how to verify cache line alignment programmatically in unit tests and benchmarks.

---

## 1. The Core Tradeoff: Memory Padding vs. CPU Cache Lines

Even though we are storing only two 8-byte unsigned 64-bit integers (`ulong WriteIndex` and `ulong ReadIndex`), we pad the struct out to **256 bytes total** so that each variable owns a complete, independent hardware cache line:

* **Intel x86_64 Xeon:** 64-byte cache line size.
* **Apple Silicon (M1/M4 ARM64):** 128-byte cache line size.

```text
Byte 0                        Byte 128                      Byte 256
┌───────────┬────────────────┬───────────┬─────────────────┐
│ WriteIndex│  Padding Space │ ReadIndex │  Padding Space  │
│  (8B)     │   (120 Bytes)  │   (8B)    │   (120 Bytes)   │
└───────────┴────────────────┴───────────┴─────────────────┘
├────────────────────────────┼─────────────────────────────┤
│   Cache Line 1 (128 Bytes) │   Cache Line 2 (128 Bytes)  │
```

---

## 2. Why Wasting 240 Bytes of Memory Is Worth It

CPUs cannot fetch or invalidate single 8-byte integers from RAM. Memory is fetched and invalidated strictly in **cache line blocks** (64B / 128B).

* **Unpadded Layout (16 Bytes Total):** `WriteIndex` and `ReadIndex` sit side-by-side on the same cache line. Every time Core 1 updates `WriteIndex`, it invalidates Core 2's cache line. Every time Core 2 updates `ReadIndex`, it invalidates Core 1's cache line. This causes **False Sharing (Cache Line Bouncing)**, stalling execution threads and inflating latency by up to 100x.
* **Padded Layout (256 Bytes Total):** `WriteIndex` (byte 0) and `ReadIndex` (byte 128) sit on separate cache lines. Neither core ever invalidates the other's L1 cache line, guaranteeing sub-50ns latency.

---

## 3. How the CLR & CPU Interconnect Memory Offsets

While the .NET CLR normally abstracts away memory management, it also provides low-level memory layout control:

1. **JIT Code Generation:** When the JIT compiler emits machine code for a struct annotated with `[StructLayout(LayoutKind.Explicit)]` and `[FieldOffset(128)]`, it generates assembly instructions with explicit byte offsets (e.g. `MOV [RAX + 128], RDX`).
2. **CPU Memory Controller Mapping:** At the hardware level, cache lines are aligned 128-byte blocks of physical RAM starting at addresses ending in multiples of 128 (`0x00`, `0x80`, `0x100`, etc.). Because `ReadIndex` is offset by 128 bytes from `WriteIndex`, the CPU hardware cache controller is physically forced to assign them to two separate L1 cache lines.
3. **Mechanical Sympathy in Managed Runtimes:** Features like `[StructLayout]` in C# (or Java's `@Contended` annotation) allow developers to harness bare-metal hardware cache alignment directly within managed code.

---

## 4. How to Verify Cache Line Alignment in C#

### Method 1: Programmatic Structural Verification (Unit Tests)
We can inspect field offsets and byte sizes programmatically using `Marshal.OffsetOf<T>()` and `Unsafe.SizeOf<T>()` directly in a unit test:

```csharp
[Fact]
public void PaddedSequence_GivenExplicitLayout_ThenSeparatesIndexesByAtLeast128Bytes()
{
    // Act
    var writeOffset = (int)Marshal.OffsetOf<PaddedSequence>(nameof(PaddedSequence.WriteIndex));
    var readOffset = (int)Marshal.OffsetOf<PaddedSequence>(nameof(PaddedSequence.ReadIndex));
    var totalSize = Unsafe.SizeOf<PaddedSequence>();

    // Assert
    writeOffset.Should().Be(0);
    readOffset.Should().BeGreaterThanOrEqualTo(128, "ReadIndex must be separated by at least 128 bytes to prevent false sharing on Apple M1/M4 and Intel Xeon cores");
    totalSize.Should().BeGreaterThanOrEqualTo(256, "PaddedSequence total size must consume 256 bytes (two 128-byte cache lines)");
}
```

### Method 2: Multi-Threaded Latency & Throughput Benchmark (BenchmarkDotNet)
To measure the real-world performance impact under contention:
* **Unpadded Version:** Shows high latency jitter (~500ns–5,000ns) due to L1 cache line invalidation stalls.
* **Padded Version:** Shows flat <50ns latency and 10x–100x higher throughput across concurrent producer/consumer threads.

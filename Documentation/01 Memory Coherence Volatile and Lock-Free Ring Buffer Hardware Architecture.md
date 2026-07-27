# Memory Coherence, Volatile, and Lock-Free SPSC Hardware Architecture

This document details how CPU cores, cache lines, memory barriers, and `Volatile` work at the hardware level in a low-latency Single Producer Single Consumer (SPSC) queue model.

---

## 1. Dual-Core Thread & Queue Execution Model

```
 ┌───────────────────────────────────────┐             ┌───────────────────────────────────────┐
 │ CPU Core 1 (Producer)                 │             │ CPU Core 2 (Consumer)                 │
 │                                       │             │                                       │
 │ 1. Read packet from network socket    │             │ 1. Busy-spin / poll tail cursor       │
 │ 2. Parse into fixed struct payload    │             │ 2. Read order payload from ring buffer│
 │ 3. Write payload to ringBuffer[tail]  │             │ 3. Execute OrderBook.Add / Cancel     │
 │ 4. Volatile.Write(ref tail, tail + 1) │             │ 4. Volatile.Write(ref head, head + 1) │
 └──────────────────┬────────────────────┘             └──────────────────▲────────────────────┘
                    │                                                     │
                    │               Shared Memory Ring Buffer             │
                    └─────────────────────────────────────────────────────┘
```

* **Core 1 (Producer):** Runs the network listener loop. Reads network packets, parses them into zero-allocation struct payloads, writes payload to `ringBuffer[tail]`, and advances `tail`.
* **Core 2 (Consumer):** Runs the matching engine loop. Continuously polls `tail` in a spin loop. When `head < tail`, it extracts the payload, executes the order against `OrderBook`, and advances `head`.

---

## 2. The Hardware Reality: CPU L1/L2 Caches & Memory Coherence

### The Problem: Local CPU Caches & Out-of-Order Execution
* Modern CPUs do not read and write directly to RAM on every instruction; doing so would take ~200 to 300 clock cycles. Instead, each core reads/writes to its private **L1 and L2 Caches** (~1 to 4 clock cycles).
* Memory is loaded into caches in blocks called **Cache Lines** (64 bytes on Intel x86_64 Xeon; 128 bytes on Apple Silicon M1/M4 ARM64).
* Without `volatile` or memory barriers:
  1. **Stale Reads:** Core 2 might continuously read `tail` from its own local L1 cache or CPU register, unaware that Core 1 updated `tail`.
  2. **Instruction Reordering:** The C# JIT compiler and CPU hardware may reorder instructions. Core 1 might update `tail` **before** the order payload bytes actually land in the array! Core 2 would then read garbage memory.

---

## 3. What `Volatile.Write` and `Volatile.Read` Do at the Hardware Level

### `Volatile.Write(ref tail, nextTail)` on Core 1 (Producer)
1. **Store Fence (Release Barrier):** Forces all preceding memory writes (the order payload in `ringBuffer[tail]`) to complete and commit **BEFORE** the `tail` write becomes visible to any other core.
2. **Cache Invalidation (MESI Protocol):** Triggers the CPU's hardware cache coherence protocol (e.g. MESI / MOESI protocol). The hardware broadcasts an invalidation signal over the CPU interconnect fabric (Intel UPI / AMD Infinity Fabric / Apple Fabric), marking the cache line containing `tail` as **Invalid** in Core 2's L1 cache.

### `Volatile.Read(ref tail)` on Core 2 (Consumer)
1. **Load Fence (Acquire Barrier):** Guarantees that subsequent memory reads (reading the payload struct from `ringBuffer[head]`) occur **AFTER** reading `tail`.
2. **Fresh Cache Line Fetch:** Because Core 1 marked Core 2's cache line for `tail` as invalid, calling `Volatile.Read(ref tail)` forces Core 2 to fetch the updated cache line over the interconnect from Core 1's L1/L2 cache or L3 shared cache into Core 2's L1 cache.

---

## 4. Cache Line Bouncing & False Sharing

If `head` (read/written by Core 2) and `tail` (read/written by Core 1) are placed next to each other in memory on the **same 64-byte or 128-byte cache line**:
* Every time Core 1 updates `tail`, it invalidates Core 2's cache line—forcing Core 2 to stall and re-fetch `head` from L3/RAM!
* Every time Core 2 updates `head`, it invalidates Core 1's cache line!

This CPU stall cycle is called **False Sharing** (Cache Line Bouncing).

### Solution: Cache Line Padding
Pad `head` and `tail` variables with 128 bytes of empty space so they reside on completely separate physical cache lines:

```csharp
[StructLayout(LayoutKind.Explicit, Size = 256)]
public struct PaddedHeadTail
{
    [FieldOffset(0)] public ulong Head;
    [FieldOffset(128)] public ulong Tail; // Sitting 128 bytes away on a separate cache line
}
```

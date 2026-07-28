# Power-of-Two Bitwise Masking vs. Modulo Division

This document explains the mathematical mechanics and CPU instruction performance behind **Power-of-Two Bitwise Masking** versus traditional **Modulo Division** in low-latency ring buffers and data structures.

---

## 1. CPU Instruction Costs

In low-latency systems (e.g. trading engines, ring buffers), hardware instruction latency dictates throughput bounds:

| Operation | C# / C++ Syntax | x86_64 CPU Instruction | Typical CPU Cycles | Latency (ns @ 3.5GHz) |
| :--- | :--- | :--- | :--- | :--- |
| **Bitwise AND** | `x & mask` | `and` | **1 cycle** | **~ 0.3 ns** |
| **Bitwise Shift** | `x >> k` | `shr` | **1 cycle** | **~ 0.3 ns** |
| **Addition / Subtraction**| `x + y` / `x - y` | `add` / `sub` | **1 cycle** | **~ 0.3 ns** |
| **Multiplication** | `x * y` | `imul` | **2 - 3 cycles** | **~ 0.8 ns** |
| **Modulo / Division** | `x % capacity` | `idiv` | **10 - 30 cycles** | **~ 5.0 - 15.0 ns** |

Executing an integer division (`idiv`) on every queue enqueue/dequeue operation adds an unnecessary ~10 to 30 clock cycle penalty per event.

---

## 2. Mathematical Mechanics: Shift, Remainder, and Bitmasking

### The Bitwise Shift & Remainder Relationship
When dividing an integer $x$ by a power of two $2^k$ (e.g., $1024 = 2^{10}$):
* **Quotient:** Shifting right by $k$ bits (`x >> k`) yields the quotient $\lfloor x / 2^k \rfloor$.
* **Remainder:** The bits that "fall off" the right side during the shift represent the exact remainder $x \pmod{2^k}$!

### Isolate the Lower $k$ Bits via Masking
To extract those lower $k$ bits in 1 single CPU instruction without performing a shift or division:
1. Constrain capacity to a power of two: $\text{capacity} = 2^k$ (e.g. $1024$).
2. Calculate mask: $\text{mask} = \text{capacity} - 1$ (e.g. $1024 - 1 = 1023$).
3. In binary, $1023$ has $1$s in all lower $k$ bits (`00111111111_2`).
4. Bitwise `AND` (`sequence & mask`) clears all bits above bit $k-1$ and preserves the lower $k$ bits: `slotIndex = sequence & (capacity - 1)`

---

## 3. Concrete Binary Worked Example

Let $\text{sequence} = 1029$ and $\text{capacity} = 1024$ ($\text{mask} = 1023$):

```binary
  10000000101  (sequence = 1029)
& 00111111111  (mask     = 1023)
─────────────
  00000000101  (slotIndex = 5)
```

$1029 \pmod{1024} = 5$, computed in **1 single CPU cycle** (`and` instruction) instead of 30 cycles (`idiv`).

---

## 4. Application in Monotonic Sequence Buffers

In high-performance ring buffers (`SpscRingBuffer<T>`):
* Sequence indices (`_writeIndex`, `_readIndex`) are 64-bit unsigned integers (`ulong`) that increment monotonically ($0, 1, 2, 3, \dots$).
* Array slot mapping is computed instantaneously: `var slot = _writeIndex & _mask;`.
* Sequence counters never reset, serving double duty as position cursors and queue occupancy indicators without requiring expensive modulo arithmetic.

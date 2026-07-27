# How To Feed Signals Into the Engine or Probe a Running Console App

In high-frequency trading and low-latency exchange design (inspired by Sourav Ghosh's C++ architecture and the LMAX Disruptor pattern), there are two primary architecture models for receiving and processing incoming orders:

---

## 1. SPSC Lock-Free Ring Buffer Model (Recommended for Real Systems)
**Single Producer Single Consumer (SPSC) Queue Model**

```
[ Network/Socket Listener ]  ──(Push Struct)──>  [ Lock-Free SPSC Ring Buffer ]  ──(Poll/Spin)──>  [ Matching Engine Thread ]
```

### Key Characteristics
1. **Thread Separation:**
   * Networking threads (or test signal generators) act as **Single Producers (SP)**.
   * They write fixed-width order structs (`ClientRequest`) directly into a pre-allocated **Lock-Free SPSC Ring Buffer**.
   * The Matching Engine runs on a **single dedicated CPU thread** acting as the **Single Consumer (SC)**, busy-spinning (or using `SpinWait`) on the ring buffer sequence cursor.
2. **Low Latency Drivers:**
   * Eliminates OS locks, context switching overhead, and thread synchronization contention.
   * Ensures deterministic execution: the engine thread never sleeps or blocks on OS primitives.
3. **Outbound Data Flow:**
   * Outbound responses (`ClientResponse` and `MarketUpdate`) are pushed into an **Outbound SPSC Ring Buffer** consumed by gateway or market data publisher threads.

---

## 2. Direct Push / Single-Threaded Event Loop (Lowest Latency)
**Direct Callback Model**

```
[ Fast Socket / Generator ]  ──(Direct Call: engine.ProcessOrder)──>  [ OrderBook ]
```

### Key Characteristics
1. **Direct Execution:**
   * The network listener or signal probe directly invokes `engine.ProcessOrder(...)` on the same thread context.
2. **Use Case:**
   * Eliminates queue hop delay entirely ($0$ inter-thread latency). Ideal for single-threaded benchmarks or zero-copy socket callbacks (e.g. kernel bypass / `io_uring` / `SocketAsyncEventArgs`).

---

## 3. How To Probe a Running Engine Console App

To feed or test order signals against a running engine console application:

1. **In-Process Signal Probe (Fastest for Benchmarking):**
   * Run a dedicated producer thread inside the console app pushing synthetic order structs into the SPSC RingBuffer at high throughput ($100\text{k}+$ ops/sec).
2. **Inter-Process Probe (Shared Memory / MMF):**
   * Use a `MemoryMappedFile` containing a shared lock-free RingBuffer. An external probe process writes to shared memory while the engine process reads with zero OS network stack overhead.
3. **UDP Binary Sockets:**
   * Send fixed-size binary structs (e.g. SBE / FIX over binary socket) over UDP. The engine parses the struct directly from `ReadOnlySpan<byte>` without string or JSON allocation overhead.

# Event Emission and Outbound Ring Buffer Architecture in Low-Latency Engines

This document details how a low-latency matching engine uses outbound **Lock-Free SPSC Ring Buffers** as asynchronous event emitters to notify external consumers without blocking the core execution core.

---

## 1. The Separation of Execution vs. Event Emission

In high-frequency trading platforms, the core matching engine thread must perform order matching in deterministic nanosecond time. It **must never** perform blocking operations such as:
* Disk file writes (`FileStream.Write` / `Console.WriteLine`).
* TCP socket pushes or network I/O.
* Heap object allocations or string formatting.

Instead, when an event occurs (e.g. order accepted, trade executed, order canceled), the matching engine writes a fixed-size value-struct event payload into a dedicated **SPSC Ring Buffer**.

---

## 2. Multi-Consumer Ring Buffer Topology

Each outbound ring buffer serves a distinct consumer running on a dedicated CPU core:

```text
 ┌───────────────────────────────────────────────────────────────────────────┐
 │ CORE 1: Core Matching Engine Thread                                       │
 │                                                                           │
 │   Processes incoming order on hot path (~20 ns)                           │
 └───────┬───────────────────────────┬───────────────────────────┬───────────┘
         │                           │                           │
         │ Push ClientResponse       │ Push MarketUpdate         │ Push LogEntry
         ▼                           ▼                           ▼
 ┌───────────────┐           ┌───────────────┐           ┌───────────────┐
 │ SPSC Buffer 1 │           │ SPSC Buffer 2 │           │ SPSC Buffer 3 │
 └───────┬───────┘           └───────┬───────┘           └───────┬───────┘
         │                           │                           │
         ▼                           ▼                           ▼
 ┌───────────────┐           ┌───────────────┐           ┌───────────────┐
 │ CORE 2        │           │ CORE 3        │           │ CORE 4        │
 │ Order Gateway │           │ Market Data   │           │ Async Logger  │
 │ (TCP Server)  │           │ (Multicast)   │           │ (Disk Stream) │
 └───────────────┘           └───────────────┘           └───────────────┘
```

---

## 3. The Three Outbound Event Channels

1. **Client Gateway Response Channel (`ClientResponse`):**
   * **Payload:** `ClientResponse(Type, ClientId, TickerId, ClientOrderId, MarketOrderId, Side, Price, ExecQty, LeavesQty)`
   * **Consumer:** Order Gateway thread sending binary TCP frames back to trading clients.
2. **Market Data Feed Channel (`MarketUpdate`):**
   * **Payload:** `MarketUpdate(Type, TickerId, Side, Price, Qty, OrderId)`
   * **Consumer:** Market Data Publisher thread broadcasting L2 depth updates via UDP Multicast.
3. **Audit Trail & Journaling Channel (`LogEntry`):**
   * **Payload:** `LogEntry(Level, TimestampTicks, TickerId, ClientId, ClientOrderId, Price, Qty)`
   * **Consumer:** `LowLatencyLogger` background thread writing state changes to disk.

---

## 4. Key Performance Guarantees

* **Zero Lock Contention:** Each ring buffer has strictly 1 producer (the Matching Engine) and 1 consumer (the dedicated worker thread).
* **Zero Heap Allocations:** All event structs (`ClientResponse`, `MarketUpdate`, `LogEntry`) pass by `in` reference on stack.
* **Deterministic Execution:** The engine posts an event signal in **~3 nanoseconds**, maintaining uniform p99.99 latency under heavy market bursts.

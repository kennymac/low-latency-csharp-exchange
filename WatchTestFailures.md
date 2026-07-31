# Watch Test Failures Log (Mutation Testing Audit)

> **Purpose:** Record of systematic fault injection (mutation testing) on branch `watchfails`. This audit tests whether unit tests, Coyote concurrency tests, and FsCheck property tests actively catch production regressions or highlight test coverage gaps.

---

## Audit Execution Results Summary

Total Mutations Evaluated: **8**
* **Killed Mutants (Caught by Tests):** **5**
* **Surviving Mutants (Coverage Gaps Identified):** **3**

---

## Detailed Mutation Audit Matrix

| # | Component | Production Mutation | Outcome | Catching Test Suite / Test Method | Analysis & Notes |
| :- | :--- | :--- | :--- | :--- | :--- |
| **1** | `SpscRingBuffer.cs` | Change mask calculation from `capacity - 1` to `capacity` | **KILLED** | `SpscRingBufferTest.TryEnqueueAndTryDequeue_GivenWrapAround`<br>`SpscRingBufferPropertyTest` (FsCheck) | Failed immediately with `IndexOutOfRangeException` upon sequence wrap-around. |
| **2** | `SpscRingBuffer.cs` | Change full check from `>= Capacity` to `> Capacity` (Off-by-one overrun) | **KILLED** | `SpscRingBufferTest.TryEnqueue_GivenFullBuffer_ThenReturnsFalse`<br>`SpscRingBufferPropertyTest` (FsCheck) | Caught by unit boundary test and FsCheck state-machine shrinking. |
| **3** | `SpscRingBuffer.cs` | Remove `Volatile.Write` on `WriteIndex` (Bypass memory barrier) | **KILLED** | `SpscRingBufferCoyoteTest` (Microsoft Coyote) | Standard unit tests missed this on strong memory hardware, but **Coyote systematic testing caught it immediately** across concurrent interleavings. |
| **4** | `OrderBook.cs` | Change ask price matching from `<` to `<=` (Refuse match on exact price) | **KILLED** | `OrderBookTest.Add_GivenMatchingBuyAndSellOrders_ThenExecutesExactFill` | Caught by basic order book exact matching unit tests. |
| **5** | `OrderBook.cs` | Comment out `_orderPool.Deallocate(order)` (MemPool node leak) | **SURVIVED** | *None* | **Coverage Gap:** Functional unit tests place < 10 orders, so 10,000-node pool exhaustion was never triggered. |
| **6** | `OrderBook.cs` | Change `fillQty = Math.Min(leavesQty, makerQty)` to `makerQty` | **KILLED** | `OrderBookTest.Add_GivenTakerOrderSweepingMultiplePrices` | Caught by partial fill and multi-level price sweep unit tests. |
| **7** | `OrderServer.cs` | Change frame length check to `< SizeOf<ClientRequest>() - 1` | **SURVIVED** | *None* | **Coverage Gap:** Unit tests only send valid frames; no test sends an undersized frame (`SizeOf - 1`). |
| **8** | `OrderServer.cs` | `TryReceiveRequest` ignores `_requestBuffer.TryEnqueue` failure | **SURVIVED** | *None* | **Coverage Gap:** Unit tests never saturate the 4,096-element inbound gateway buffer to verify full-buffer rejection. |

---

## Action Plan for Identified Coverage Gaps

To remediate the **3 Surviving Mutants**:

1. **Fix for Mutant 5 (MemPool Leak):** Add `OrderBookTest.Add_GivenPoolExhaustion_ThenRecyclesNodesWithoutLeak()` that executes > 10,000 order fills and asserts node pool availability.
2. **Fix for Mutant 7 (Undersized Binary Frame):** Add `OrderServerTest.TryReceiveRequest_GivenUndersizedFrame_ThenReturnsFalse()` passing `Span<byte>` of length `SizeOf<ClientRequest>() - 1`.
3. **Fix for Mutant 8 (Gateway Inbound Saturation):** Add `OrderServerTest.TryReceiveRequest_GivenFullInboundBuffer_ThenReturnsFalse()` filling 4,096 requests and verifying overflow rejection.

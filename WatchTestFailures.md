# Watch Test Failures Log (Mutation Testing Audit)

> **Purpose:** Record of systematic fault injection (mutation testing) on branch `watchfails`. This audit tests whether unit tests, Coyote concurrency tests, and FsCheck property tests actively catch production regressions or highlight test coverage gaps.

---

## Audit Execution Results Summary

Total Mutations Evaluated: **8**
* **Initially Killed Mutants:** **5**
* **Initially Surviving Mutants (Coverage Gaps Identified):** **3**
* **Remediated & Killed Mutants (Final State):** **8 / 8 (100% Mutation Coverage)**

---

## Detailed Mutation Audit Log

| # | Component | Production Mutation | Initial Outcome | Catching Test Suite / Test Method | Final State |
| :- | :--- | :--- | :--- | :--- | :--- |
| **1** | `SpscRingBuffer.cs` | Change mask calculation from `capacity - 1` to `capacity` | **KILLED** | `SpscRingBufferTest.TryEnqueueAndTryDequeue_GivenWrapAround`<br>`SpscRingBufferPropertyTest` (FsCheck) | **KILLED** |
| **2** | `SpscRingBuffer.cs` | Change full check from `>= Capacity` to `> Capacity` (Off-by-one overrun) | **KILLED** | `SpscRingBufferTest.TryEnqueue_GivenFullBuffer_ThenReturnsFalse`<br>`SpscRingBufferPropertyTest` (FsCheck) | **KILLED** |
| **3** | `SpscRingBuffer.cs` | Remove `Volatile.Write` on `WriteIndex` (Bypass memory barrier) | **KILLED** | `SpscRingBufferCoyoteTest` (Microsoft Coyote) | **KILLED** (Coyote systematic testing caught concurrency barrier bypass) |
| **4** | `OrderBook.cs` | Change ask price matching from `<` to `<=` (Refuse match on exact price) | **KILLED** | `OrderBookTest.Add_GivenMatchingBuyAndSellOrders_ThenExecutesExactFill` | **KILLED** |
| **5** | `OrderBook.cs` | Comment out `_orderPool.Deallocate(order)` (MemPool node leak) | **SURVIVED** *(Gap)* | `OrderBookTest.Add_GivenHighVolumeOrderCycle_ThenRecyclesPoolNodesWithoutLeaking` | **REMEDIATED & KILLED** |
| **6** | `OrderBook.cs` | Change `fillQty = Math.Min(leavesQty, makerQty)` to `makerQty` | **KILLED** | `OrderBookTest.Add_GivenTakerOrderSweepingMultiplePrices` | **KILLED** |
| **7** | `OrderServer.cs` | Change frame length check to `< SizeOf<ClientRequest>() - 1` | **SURVIVED** *(Gap)* | `OrderServerTest.TryReceiveRequest_GivenUndersizedBinaryFrame_ThenReturnsFalse` | **REMEDIATED & KILLED** |
| **8** | `OrderServer.cs` | `TryReceiveRequest` ignores `_requestBuffer.TryEnqueue` failure | **SURVIVED** *(Gap)* | `OrderServerTest.TryReceiveRequest_GivenFullInboundBuffer_ThenReturnsFalse` | **REMEDIATED & KILLED** |

---

## Remediation Summary

1. **Mutant 5 (MemPool Leak):** Added `OrderBookTest.Add_GivenHighVolumeOrderCycle_ThenRecyclesPoolNodesWithoutLeaking()` running 40,000 orders to verify object pool node recycling.
2. **Mutant 7 (Undersized Binary Frame):** Added `OrderServerTest.TryReceiveRequest_GivenUndersizedBinaryFrame_ThenReturnsFalse()` passing a `Span<byte>` short frame (`SizeOf - 1`).
3. **Mutant 8 (Gateway Inbound Saturation):** Added `OrderServerTest.TryReceiveRequest_GivenFullInboundBuffer_ThenReturnsFalse()` filling the 16-slot inbound gateway buffer to verify overflow rejection.

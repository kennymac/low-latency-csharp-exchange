using BenchmarkDotNet.Attributes;
using LowLatency.ScratchPad.Engine;
using LowLatency.ScratchPad.Engine.Model;
using LowLatency.ScratchPad.IdiomaticEngine;

namespace LowLatency.ScratchPad.Benchmarks;

[MemoryDiagnoser]
public class MatchingEngineBenchmark
{
    private OrderBook _zeroAllocBook = null!;
    private IdiomaticOrderBook _idiomaticBook = null!;
    private ulong _orderIdCounter;

    [GlobalSetup]
    public void Setup()
    {
        _zeroAllocBook = new OrderBook(tickerId: 1, clientResponseListener: null, marketUpdateListener: null);
        _idiomaticBook = new IdiomaticOrderBook(tickerId: 1, clientResponseListener: null, marketUpdateListener: null);
        _orderIdCounter = 100;
    }

    [Benchmark(Baseline = true)]
    public void MatchOrderPair_ZeroAllocation()
    {
        var id = ++_orderIdCounter;
        
        // Zero-Allocation Engine: Add Sell order (10 @ 100) and match with Buy order (10 @ 100).
        // Recycles Order nodes back to MemPool<Order> with 0 Managed Heap allocations.
        _zeroAllocBook.Add(clientId: 1, clientOrderId: id, side: Side.Sell, price: 100, qty: 10);
        _zeroAllocBook.Add(clientId: 2, clientOrderId: id + 10_000_000, side: Side.Buy, price: 100, qty: 10);
    }

    [Benchmark]
    public void MatchOrderPair_IdiomaticCSharp()
    {
        var id = ++_orderIdCounter;

        // Idiomatic C# Engine: Standard Heap-allocated class Order, SortedDictionary, and LinkedList.
        _idiomaticBook.Add(clientId: 1, clientOrderId: id, side: IdiomaticEngine.Model.Side.Sell, price: 100, qty: 10);
        _idiomaticBook.Add(clientId: 2, clientOrderId: id + 10_000_000, side: IdiomaticEngine.Model.Side.Buy, price: 100, qty: 10);
    }
}

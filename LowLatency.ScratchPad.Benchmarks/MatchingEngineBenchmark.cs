using BenchmarkDotNet.Attributes;
using LowLatency.ScratchPad.Engine;
using LowLatency.ScratchPad.Engine.Model;

namespace LowLatency.ScratchPad.Benchmarks;

[MemoryDiagnoser]
public class MatchingEngineBenchmark
{
    private OrderBook _book = null!;
    private ulong _orderIdCounter;

    [GlobalSetup]
    public void Setup()
    {
        _book = new OrderBook(tickerId: 1, clientResponseListener: null, marketUpdateListener: null);
        _orderIdCounter = 100;
    }

    [Benchmark]
    public void MatchOrderPair()
    {
        var id = ++_orderIdCounter;
        
        // Add a Sell order (10 @ 100) and immediately match it with a Buy order (10 @ 100).
        // Complete fills recycle both Order nodes back to MemPool<Order>, enabling millions of 0-allocation benchmark iterations.
        _book.Add(clientId: 1, clientOrderId: id, side: Side.Sell, price: 100, qty: 10);
        _book.Add(clientId: 2, clientOrderId: id + 10_000_000, side: Side.Buy, price: 100, qty: 10);
    }
}

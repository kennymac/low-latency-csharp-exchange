using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using LowLatency.ScratchPad.Engine;
using LowLatency.ScratchPad.Engine.Model;

namespace LowLatency.ScratchPad.Benchmarks;

[MemoryDiagnoser]
public class OrderServerBenchmark
{
    private OrderServer _server = null!;
    private byte[] _binaryFrame = null!;

    [GlobalSetup]
    public void Setup()
    {
        _server = new OrderServer(inboundCapacity: 16_384, outboundCapacity: 16_384);
        var req = new ClientRequest(ClientId: 1, ClientOrderId: 101, TickerId: 1, Side: Side.Buy, Price: 150, Qty: 100);

        _binaryFrame = new byte[Unsafe.SizeOf<ClientRequest>()];
        MemoryMarshal.Write(_binaryFrame, in req);
    }

    [Benchmark]
    public void DecodeAndEnqueueBinaryFrame()
    {
        _server.TryReceiveRequest(binaryFrame: _binaryFrame, out _);
        _server.TryDequeueRequest(out _);
    }
}

#pragma warning disable CS8602

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LowLatency.ScratchPad.Engine;
using LowLatency.ScratchPad.Engine.Model;

namespace LowLatency.ScratchPad.MatchingEngine.UnitTests;

public class OrderServerTest
{
    [Fact]
    public void TryReceiveRequest_GivenValidBinaryFrame_ThenDecodesClientRequestCorrectly()
    {
        // Arrange
        var server = new OrderServer(inboundCapacity: 16, outboundCapacity: 16);
        var expectedRequest = new ClientRequest(
            ClientId: 1,
            ClientOrderId: 101,
            TickerId: 10,
            Side: Side.Buy,
            Price: 150,
            Qty: 100);

        Span<byte> binaryFrame = stackalloc byte[Unsafe.SizeOf<ClientRequest>()];
        MemoryMarshal.Write(binaryFrame, in expectedRequest);

        // Act
        var receivedSuccess = server.TryReceiveRequest(binaryFrame: binaryFrame, out var parsedRequest);
        var dequeueSuccess = server.TryDequeueRequest(out var dequeuedRequest);

        // Assert
        receivedSuccess.Should().BeTrue();
        dequeueSuccess.Should().BeTrue();
        dequeuedRequest.ClientId.Should().Be(1);
        dequeuedRequest.ClientOrderId.Should().Be(101);
        dequeuedRequest.TickerId.Should().Be(10);
        dequeuedRequest.Side.Should().Be(Side.Buy);
        dequeuedRequest.Price.Should().Be(150);
        dequeuedRequest.Qty.Should().Be(100);
    }

    [Fact]
    public void FormatResponseFrame_GivenClientResponse_ThenEncodesBinaryFrameCorrectly()
    {
        // Arrange
        var response = new ClientResponse(
            Type: ClientResponseType.Filled,
            ClientId: 1,
            TickerId: 10,
            ClientOrderId: 101,
            MarketOrderId: 5_001,
            Side: Side.Buy,
            Price: 150,
            ExecQty: 50,
            LeavesQty: 50);

        Span<byte> destination = stackalloc byte[Unsafe.SizeOf<ClientResponse>()];

        // Act
        var writtenBytes = OrderServer.FormatResponseFrame(response: response, destination: destination);
        var decodedResponse = MemoryMarshal.Read<ClientResponse>(destination);

        // Assert
        writtenBytes.Should().Be(Unsafe.SizeOf<ClientResponse>());
        decodedResponse.Type.Should().Be(ClientResponseType.Filled);
        decodedResponse.ClientId.Should().Be(1);
        decodedResponse.ClientOrderId.Should().Be(101);
        decodedResponse.ExecQty.Should().Be(50);
    }

    [Fact]
    public void EnqueueRequestAndTryDequeue_GivenMultipleRequests_ThenTransfersLockFreelyInFifoOrder()
    {
        // Arrange
        var server = new OrderServer(inboundCapacity: 16, outboundCapacity: 16);
        var req1 = new ClientRequest(ClientId: 1, ClientOrderId: 1, TickerId: 1, Side: Side.Buy, Price: 100, Qty: 10);
        var req2 = new ClientRequest(ClientId: 2, ClientOrderId: 2, TickerId: 1, Side: Side.Sell, Price: 105, Qty: 5);

        // Act
        server.EnqueueRequest(request: req1);
        server.EnqueueRequest(request: req2);

        var success1 = server.TryDequeueRequest(out var dequeued1);
        var success2 = server.TryDequeueRequest(out var dequeued2);

        // Assert
        success1.Should().BeTrue();
        dequeued1.ClientId.Should().Be(1);

        success2.Should().BeTrue();
        dequeued2.ClientId.Should().Be(2);
    }

    [Fact]
    public void TryReceiveRequest_GivenBulkWireFrames_ThenZeroGcAllocations()
    {
        // Arrange
        var server = new OrderServer(inboundCapacity: 16_384, outboundCapacity: 16_384);
        var sampleRequest = new ClientRequest(ClientId: 1, ClientOrderId: 1, TickerId: 1, Side: Side.Buy, Price: 100, Qty: 10);

        Span<byte> binaryFrame = stackalloc byte[Unsafe.SizeOf<ClientRequest>()];
        MemoryMarshal.Write(binaryFrame, in sampleRequest);

        // Warm-up phase: JIT compile network parsing path
        for (var i = 0; i < 50; i++)
        {
            server.TryReceiveRequest(binaryFrame: binaryFrame, out _);
            server.TryDequeueRequest(out _);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();

        // Act: Parse and process 10,000 binary wire frames on hot path
        for (var i = 0; i < 10_000; i++)
        {
            server.TryReceiveRequest(binaryFrame: binaryFrame, out _);
            server.TryDequeueRequest(out _);
        }

        var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        var totalAllocatedBytes = bytesAfter - bytesBefore;

        // Assert zero bytes allocated on the managed heap
        totalAllocatedBytes.Should().Be(0, "no bytes should be allocated to the managed heap");
    }
}

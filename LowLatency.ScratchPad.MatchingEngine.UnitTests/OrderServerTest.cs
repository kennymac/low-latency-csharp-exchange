#pragma warning disable CS8602

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LowLatency.ScratchPad.Engine;
using LowLatency.ScratchPad.Engine.Model;
using Xunit;
using AwesomeAssertions;

namespace LowLatency.ScratchPad.MatchingEngine.UnitTests;

public class OrderServerTest
{
    [Fact]
    public void TryReceiveRequest_GivenValidBinaryFrame_ThenDecodesClientRequestCorrectly()
    {
        // Arrange
        var server = new OrderServer(inboundCapacity: 16, outboundCapacity: 16);
        var expectedRequest = new ClientRequest(
            clientId: 1,
            clientOrderId: 101,
            tickerId: 10,
            side: Side.Buy,
            price: 150,
            qty: 100);

        Span<byte> binaryFrame = stackalloc byte[Unsafe.SizeOf<ClientRequest>()];
        MemoryMarshal.Write(binaryFrame, in expectedRequest);

        // Act
        var receivedSuccess = server.TryReceiveRequest(binaryFrame: binaryFrame, out var parsedRequest);
        var dequeueSuccess = server.TryDequeueRequest(out var dequeuedRequest);

        // Assert
        receivedSuccess.Should().BeTrue();
        dequeueSuccess.Should().BeTrue();
        dequeuedRequest.clientId.Should().Be(1);
        dequeuedRequest.clientOrderId.Should().Be(101);
        dequeuedRequest.tickerId.Should().Be(10);
        dequeuedRequest.side.Should().Be(Side.Buy);
        dequeuedRequest.price.Should().Be(150);
        dequeuedRequest.qty.Should().Be(100);
    }

    [Fact]
    public void FormatResponseFrame_GivenClientResponse_ThenEncodesBinaryFrameCorrectly()
    {
        // Arrange
        var response = new ClientResponse(
            type: ClientResponseType.Filled,
            clientId: 1,
            tickerId: 10,
            clientOrderId: 101,
            marketOrderId: 5_001,
            side: Side.Buy,
            price: 150,
            execQty: 50,
            leavesQty: 50);

        Span<byte> destination = stackalloc byte[Unsafe.SizeOf<ClientResponse>()];

        // Act
        var writtenBytes = OrderServer.FormatResponseFrame(response: response, destination: destination);
        var decodedResponse = MemoryMarshal.Read<ClientResponse>(destination);

        // Assert
        writtenBytes.Should().Be(Unsafe.SizeOf<ClientResponse>());
        decodedResponse.type.Should().Be(ClientResponseType.Filled);
        decodedResponse.clientId.Should().Be(1);
        decodedResponse.clientOrderId.Should().Be(101);
        decodedResponse.execQty.Should().Be(50);
    }

    [Fact]
    public void EnqueueRequestAndTryDequeue_GivenMultipleRequests_ThenTransfersLockFreelyInFifoOrder()
    {
        // Arrange
        var server = new OrderServer(inboundCapacity: 16, outboundCapacity: 16);
        var req1 = new ClientRequest(clientId: 1, clientOrderId: 1, tickerId: 1, side: Side.Buy, price: 100, qty: 10);
        var req2 = new ClientRequest(clientId: 2, clientOrderId: 2, tickerId: 1, side: Side.Sell, price: 105, qty: 5);

        // Act
        server.EnqueueRequest(request: req1);
        server.EnqueueRequest(request: req2);

        var success1 = server.TryDequeueRequest(out var dequeued1);
        var success2 = server.TryDequeueRequest(out var dequeued2);

        // Assert
        success1.Should().BeTrue();
        dequeued1.clientId.Should().Be(1);

        success2.Should().BeTrue();
        dequeued2.clientId.Should().Be(2);
    }

    [Fact]
    public void TryReceiveRequest_GivenBulkWireFrames_ThenZeroGcAllocations()
    {
        // Arrange
        var server = new OrderServer(inboundCapacity: 16_384, outboundCapacity: 16_384);
        var sampleRequest = new ClientRequest(clientId: 1, clientOrderId: 1, tickerId: 1, side: Side.Buy, price: 100, qty: 10);

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

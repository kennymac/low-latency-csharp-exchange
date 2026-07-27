using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LowLatency.ScratchPad.Engine.Model;

namespace LowLatency.ScratchPad.Engine;

public sealed class OrderServer
{
    private readonly SpscRingBuffer<ClientRequest> _requestBuffer;
    private readonly SpscRingBuffer<ClientResponse> _responseBuffer;

    public int RequestCount => _requestBuffer.Count;

    public int ResponseCount => _responseBuffer.Count;

    public OrderServer(int inboundCapacity = 4_096, int outboundCapacity = 4_096)
    {
        _requestBuffer = new SpscRingBuffer<ClientRequest>(capacity: inboundCapacity);
        _responseBuffer = new SpscRingBuffer<ClientResponse>(capacity: outboundCapacity);
    }

    public bool TryReceiveRequest(ReadOnlySpan<byte> binaryFrame, out ClientRequest request)
    {
        if (binaryFrame.Length < Unsafe.SizeOf<ClientRequest>())
        {
            request = default;
            return false;
        }

        request = MemoryMarshal.Read<ClientRequest>(binaryFrame);
        return _requestBuffer.TryEnqueue(in request);
    }

    public bool EnqueueRequest(in ClientRequest request)
    {
        return _requestBuffer.TryEnqueue(in request);
    }

    public bool TryDequeueRequest(out ClientRequest request)
    {
        return _requestBuffer.TryDequeue(out request);
    }

    public bool EnqueueResponse(in ClientResponse response)
    {
        return _responseBuffer.TryEnqueue(in response);
    }

    public bool TryDequeueResponse(out ClientResponse response)
    {
        return _responseBuffer.TryDequeue(out response);
    }

    public static int FormatResponseFrame(in ClientResponse response, Span<byte> destination)
    {
        var requiredBytes = Unsafe.SizeOf<ClientResponse>();
        if (destination.Length < requiredBytes)
        {
            return 0;
        }

        MemoryMarshal.Write(destination, in response);
        return requiredBytes;
    }
}

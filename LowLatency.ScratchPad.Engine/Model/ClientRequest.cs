namespace LowLatency.ScratchPad.Engine.Model;

public readonly record struct ClientRequest(
    uint clientId,
    ulong clientOrderId,
    uint tickerId,
    Side side,
    long price,
    uint qty
);

namespace LowLatency.ScratchPad.Engine.Model;

public readonly record struct ClientResponse(
    ClientResponseType type,
    uint clientId,
    uint tickerId,
    ulong clientOrderId,
    ulong marketOrderId,
    Side side,
    long price,
    uint execQty,
    uint leavesQty
);

namespace LowLatency.ScratchPad.Engine.Model;

public readonly record struct ClientResponse(
    ClientResponseType Type,
    uint ClientId,
    uint TickerId,
    ulong ClientOrderId,
    ulong MarketOrderId,
    Side Side,
    long Price,
    uint ExecQty,
    uint LeavesQty
);

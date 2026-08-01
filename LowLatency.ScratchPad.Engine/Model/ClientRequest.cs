namespace LowLatency.ScratchPad.Engine.Model;

public readonly record struct ClientRequest(
    uint ClientId,
    ulong ClientOrderId,
    uint TickerId,
    Side Side,
    long Price,
    uint Qty
);

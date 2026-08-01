namespace LowLatency.ScratchPad.Engine.Model;

public readonly record struct MarketUpdate(
    MarketUpdateType Type,
    ulong MarketOrderId,
    uint TickerId,
    Side Side,
    long Price,
    uint Qty,
    ulong Priority
);

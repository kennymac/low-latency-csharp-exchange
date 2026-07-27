namespace LowLatency.ScratchPad.Engine.Model;

public readonly record struct MarketUpdate(
    MarketUpdateType type,
    ulong marketOrderId,
    uint tickerId,
    Side side,
    long price,
    uint qty,
    ulong priority
);

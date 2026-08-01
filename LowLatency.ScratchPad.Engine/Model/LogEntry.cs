namespace LowLatency.ScratchPad.Engine.Model;

public readonly record struct LogEntry(
    LogLevel Level,
    ulong TimestampTicks,
    uint TickerId,
    uint ClientId,
    ulong ClientOrderId,
    long Price,
    uint Qty
);

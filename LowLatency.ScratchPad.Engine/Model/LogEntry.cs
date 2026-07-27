namespace LowLatency.ScratchPad.Engine.Model;

public readonly record struct LogEntry(
    LogLevel level,
    ulong timestampTicks,
    uint tickerId,
    uint clientId,
    ulong clientOrderId,
    long price,
    uint qty
);

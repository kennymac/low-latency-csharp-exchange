using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LowLatency.ScratchPad.Engine.Model;

namespace LowLatency.ScratchPad.Engine;

public sealed class LowLatencyLogger : IDisposable
{
    private readonly SpscRingBuffer<LogEntry> _ringBuffer;
    private readonly TextWriter _writer;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task? _flusherTask;

    public LowLatencyLogger(
        TextWriter writer,
        int ringCapacity = 4_096,
        bool startBackgroundFlusher = true)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _ringBuffer = new SpscRingBuffer<LogEntry>(capacity: ringCapacity);

        if (startBackgroundFlusher)
        {
            _flusherTask = Task.Run(() => BackgroundFlushLoop(_cts.Token));
        }
    }

    public bool Log(
        LogLevel level,
        uint tickerId,
        uint clientId,
        ulong clientOrderId,
        long price,
        uint qty)
    {
        var entry = new LogEntry(
            level: level,
            timestampTicks: (ulong)DateTime.UtcNow.Ticks,
            tickerId: tickerId,
            clientId: clientId,
            clientOrderId: clientOrderId,
            price: price,
            qty: qty);

        return _ringBuffer.TryEnqueue(in entry);
    }

    public int Flush()
    {
        Span<LogEntry> batch = stackalloc LogEntry[64];
        var totalFlushed = 0;

        int dequeued;
        while ((dequeued = _ringBuffer.TryDequeueBatch(destination: batch)) > 0)
        {
            for (var i = 0; i < dequeued; i++)
            {
                var entry = batch[i];
                WriteEntryToStream(entry: entry);
            }
            totalFlushed += dequeued;
        }

        _writer.Flush();
        return totalFlushed;
    }

    private void WriteEntryToStream(in LogEntry entry)
    {
        _writer.Write('[');
        _writer.Write(entry.level.ToString());
        _writer.Write("] Ticker:");
        _writer.Write(entry.tickerId);
        _writer.Write(" Client:");
        _writer.Write(entry.clientId);
        _writer.Write(" OID:");
        _writer.Write(entry.clientOrderId);
        _writer.Write(" Price:");
        _writer.Write(entry.price);
        _writer.Write(" Qty:");
        _writer.WriteLine(entry.qty);
    }

    private void BackgroundFlushLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (!_ringBuffer.IsEmpty)
            {
                Flush();
            }
            else
            {
                Thread.SpinWait(100);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _flusherTask?.Wait(500);
        }
        catch
        {
            // Ignore cancellation exceptions during shutdown
        }

        Flush();
        _cts.Dispose();
    }
}

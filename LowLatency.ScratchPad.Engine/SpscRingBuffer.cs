using System.Runtime.InteropServices;

namespace LowLatency.ScratchPad.Engine;

// Cache line padding: WriteIndex at offset 0, ReadIndex at offset 128 (128-byte cache line separation)
[StructLayout(LayoutKind.Explicit, Size = 256)]
public struct PaddedSequence
{
    [FieldOffset(0)]
    public ulong WriteIndex;

    [FieldOffset(128)]
    public ulong ReadIndex;
}

public sealed class SpscRingBuffer<T>
{
    private readonly T[] _buffer;
    private readonly int _mask;

    private PaddedSequence _sequence;

    public int Capacity => _buffer.Length;

    public int Count
    {
        get
        {
            var write = Volatile.Read(ref _sequence.WriteIndex);
            var read = Volatile.Read(ref _sequence.ReadIndex);
            return (int)(write - read);
        }
    }

    public bool IsEmpty => Count == 0;

    public bool IsFull => Count == Capacity;

    public SpscRingBuffer(int capacity)
    {
        // Fast-fail at startup if capacity is not a positive power of 2
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
        {
            throw new ArgumentException("Capacity must be a positive power of 2 (e.g. 256, 512, 1024, 2048).", nameof(capacity));
        }

        _buffer = new T[capacity];
        _mask = capacity - 1;
    }

    public bool TryEnqueue(in T item)
    {
        var write = _sequence.WriteIndex;
        var read = Volatile.Read(ref _sequence.ReadIndex);

        if ((int)(write - read) >= Capacity)
        {
            return false;
        }

        _buffer[write & (ulong)_mask] = item;
        Volatile.Write(ref _sequence.WriteIndex, write + 1);
        return true;
    }

    public bool TryDequeue(out T item)
    {
        var read = _sequence.ReadIndex;
        var write = Volatile.Read(ref _sequence.WriteIndex);

        if (read >= write)
        {
            item = default!;
            return false;
        }

        item = _buffer[read & (ulong)_mask];
        _buffer[read & (ulong)_mask] = default!; // Clear reference for GC hygiene
        Volatile.Write(ref _sequence.ReadIndex, read + 1);
        return true;
    }

    public int TryDequeueBatch(Span<T> destination)
    {
        var read = _sequence.ReadIndex;
        var write = Volatile.Read(ref _sequence.WriteIndex);

        var available = (int)(write - read);
        if (available <= 0)
        {
            return 0;
        }

        var toProcess = Math.Min(available, destination.Length);
        for (var i = 0; i < toProcess; i++)
        {
            var idx = (read + (ulong)i) & (ulong)_mask;
            destination[i] = _buffer[idx];
            _buffer[idx] = default!;
        }

        Volatile.Write(ref _sequence.ReadIndex, read + (ulong)toProcess);
        return toProcess;
    }
}

namespace LowLatency.ScratchPad.Engine;

public sealed class MemPool<T> where T : class, new()
{
    private readonly T[] _pool;
    private int _nextAvailable;

    public MemPool(int capacity)
    {
        _pool = new T[capacity];
        for (int i = 0; i < capacity; i++)
        {
            _pool[i] = new T();
        }
        _nextAvailable = capacity - 1;
    }

    public T Allocate()
    {
        if (_nextAvailable < 0)
        {
            throw new InvalidOperationException($"MemPool of {typeof(T).Name} exhausted.");
        }
        var item = _pool[_nextAvailable];
        _nextAvailable--;
        return item;
    }

    public void Deallocate(T item)
    {
        if (_nextAvailable < _pool.Length - 1)
        {
            _nextAvailable++;
            _pool[_nextAvailable] = item;
        }
    }
}

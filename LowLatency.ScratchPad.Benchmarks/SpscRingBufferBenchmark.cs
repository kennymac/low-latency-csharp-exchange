using System.Collections.Concurrent;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using LowLatency.ScratchPad.Engine;

namespace LowLatency.ScratchPad.Benchmarks;

[MemoryDiagnoser]
public class SpscRingBufferBenchmark
{
    private SpscRingBuffer<int> _spscBuffer = null!;
    private ConcurrentQueue<int> _concurrentQueue = null!;
    private Channel<int> _channel = null!;
    private ChannelWriter<int> _channelWriter = null!;
    private ChannelReader<int> _channelReader = null!;

    [GlobalSetup]
    public void Setup()
    {
        _spscBuffer = new SpscRingBuffer<int>(capacity: 1_024);
        _concurrentQueue = new ConcurrentQueue<int>();
        _channel = Channel.CreateBounded<int>(new BoundedChannelOptions(1_024)
        {
            SingleWriter = true,
            SingleReader = true
        });
        _channelWriter = _channel.Writer;
        _channelReader = _channel.Reader;
    }

    [Benchmark(Baseline = true)]
    public void SpscRingBuffer_PushAndPop()
    {
        _spscBuffer.TryEnqueue(item: 42);
        _spscBuffer.TryDequeue(out _);
    }

    [Benchmark]
    public void ConcurrentQueue_PushAndPop()
    {
        _concurrentQueue.Enqueue(42);
        _concurrentQueue.TryDequeue(out _);
    }

    [Benchmark]
    public void SystemThreadingChannel_PushAndPop()
    {
        _channelWriter.TryWrite(42);
        _channelReader.TryRead(out _);
    }
}

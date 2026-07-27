#pragma warning disable CS8602
#pragma warning disable xUnit1051

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LowLatency.ScratchPad.Engine;
using Xunit;
using AwesomeAssertions;

namespace LowLatency.ScratchPad.MatchingEngine.UnitTests;

public class SpscRingBufferTest
{
    [Fact]
    public void PaddedSequence_GivenExplicitLayout_ThenSeparatesIndexesByAtLeast128Bytes()
    {
        // Act
        var writeOffset = (int)Marshal.OffsetOf<PaddedSequence>(nameof(PaddedSequence.WriteIndex));
        var readOffset = (int)Marshal.OffsetOf<PaddedSequence>(nameof(PaddedSequence.ReadIndex));
        var totalSize = Unsafe.SizeOf<PaddedSequence>();

        // Assert
        writeOffset.Should().Be(0);
        readOffset.Should().BeGreaterThanOrEqualTo(128, "ReadIndex must be separated by at least 128 bytes to prevent false sharing on Apple M1/M4 and Intel Xeon cores");
        totalSize.Should().BeGreaterThanOrEqualTo(256, "PaddedSequence total size must consume 256 bytes (two 128-byte cache lines)");
    }

    [Fact]
    public void Constructor_GivenNonPowerOfTwoCapacity_ThenThrowsArgumentException()
    {
        // Arrange & Act
        Action act = () => _ = new SpscRingBuffer<int>(capacity: 100);

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithMessage("*Capacity must be a positive power of 2*");
    }

    [Fact]
    public void TryEnqueue_GivenEmptyBuffer_ThenItemIsEnqueuedSuccessfully()
    {
        // Arrange
        var buffer = new SpscRingBuffer<int>(capacity: 4);

        // Act
        var result = buffer.TryEnqueue(item: 42);

        // Assert
        result.Should().BeTrue();
        buffer.Count.Should().Be(1);
        buffer.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void TryDequeue_GivenEmptyBuffer_ThenReturnsFalse()
    {
        // Arrange
        var buffer = new SpscRingBuffer<int>(capacity: 4);

        // Act
        var result = buffer.TryDequeue(out var item);

        // Assert
        result.Should().BeFalse();
        item.Should().Be(0);
    }

    [Fact]
    public void TryEnqueue_GivenFullBuffer_ThenReturnsFalse()
    {
        // Arrange
        var buffer = new SpscRingBuffer<int>(capacity: 4);
        buffer.TryEnqueue(item: 1);
        buffer.TryEnqueue(item: 2);
        buffer.TryEnqueue(item: 3);
        buffer.TryEnqueue(item: 4);

        // Act
        var result = buffer.TryEnqueue(item: 5);

        // Assert
        result.Should().BeFalse();
        buffer.IsFull.Should().BeTrue();
    }

    [Fact]
    public void TryEnqueueAndTryDequeue_GivenMultipleItems_ThenPreservesFifoOrder()
    {
        // Arrange
        var buffer = new SpscRingBuffer<int>(capacity: 8);
        buffer.TryEnqueue(item: 10);
        buffer.TryEnqueue(item: 20);
        buffer.TryEnqueue(item: 30);

        // Act
        var success1 = buffer.TryDequeue(out var item1);
        var success2 = buffer.TryDequeue(out var item2);
        var success3 = buffer.TryDequeue(out var item3);

        // Assert
        success1.Should().BeTrue();
        item1.Should().Be(10);

        success2.Should().BeTrue();
        item2.Should().Be(20);

        success3.Should().BeTrue();
        item3.Should().Be(30);

        buffer.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TryEnqueueAndTryDequeue_GivenWrapAround_ThenCorrectlyHandlesPowerOfTwoIndexMasking()
    {
        // Arrange
        var buffer = new SpscRingBuffer<int>(capacity: 4);

        // Act & Assert: Wrap around multiple times
        for (var cycle = 0; cycle < 100; cycle++)
        {
            buffer.TryEnqueue(item: cycle);
            var success = buffer.TryDequeue(out var item);

            success.Should().BeTrue();
            item.Should().Be(cycle);
        }

        buffer.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TryDequeueBatch_GivenMultipleEnqueuedItems_ThenDrainsBatchInSinglePass()
    {
        // Arrange
        var buffer = new SpscRingBuffer<int>(capacity: 8);
        buffer.TryEnqueue(item: 100);
        buffer.TryEnqueue(item: 200);
        buffer.TryEnqueue(item: 300);

        Span<int> batch = stackalloc int[4];

        // Act
        var dequeuedCount = buffer.TryDequeueBatch(destination: batch);

        // Assert
        dequeuedCount.Should().Be(3);
        batch[0].Should().Be(100);
        batch[1].Should().Be(200);
        batch[2].Should().Be(300);
        buffer.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task TryEnqueueAndTryDequeue_GivenConcurrentProducerConsumerThreads_ThenTransfersAllItemsWithoutLoss()
    {
        // Arrange
        const int itemCapacity = 1_024;
        const int totalItems = 100_000;
        var buffer = new SpscRingBuffer<int>(capacity: itemCapacity);

        // Act
        var producerTask = Task.Run(() =>
        {
            for (var i = 0; i < totalItems; i++)
            {
                while (!buffer.TryEnqueue(item: i))
                {
                    // Spin-wait until space is available
                }
            }
        });

        var receivedItems = new int[totalItems];
        var consumerTask = Task.Run(() =>
        {
            var received = 0;
            while (received < totalItems)
            {
                if (buffer.TryDequeue(out var item))
                {
                    receivedItems[received++] = item;
                }
            }
        });

        await Task.WhenAll(producerTask, consumerTask);

        // Assert
        buffer.IsEmpty.Should().BeTrue();
        for (var i = 0; i < totalItems; i++)
        {
            receivedItems[i].Should().Be(i);
        }
    }

    [Fact]
    public void TryEnqueueAndTryDequeue_GivenBulkConcurrentFlow_ThenZeroGcAllocations()
    {
        // Arrange
        var buffer = new SpscRingBuffer<int>(capacity: 1_024);

        // Warm-up phase: run cycle to JIT compile methods and warm up internal memory pools
        for (var i = 0; i < 50; i++)
        {
            buffer.TryEnqueue(item: i);
            buffer.TryDequeue(out _);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();

        // Act: Enqueue and Dequeue 10,000 items on hot path
        for (var i = 0; i < 10_000; i++)
        {
            buffer.TryEnqueue(item: i);
            buffer.TryDequeue(out _);
        }

        var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        var totalAllocatedBytes = bytesAfter - bytesBefore;

        // Assert zero bytes allocated on the managed heap
        totalAllocatedBytes.Should().Be(0, "no bytes should be allocated to the managed heap");
    }
}

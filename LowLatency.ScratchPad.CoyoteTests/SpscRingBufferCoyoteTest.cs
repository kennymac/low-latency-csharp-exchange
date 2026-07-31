using FluentAssertions;
using LowLatency.ScratchPad.Engine;
using Microsoft.Coyote;
using Microsoft.Coyote.SystematicTesting;
using Xunit;

namespace LowLatency.ScratchPad.CoyoteTests;

public class SpscRingBufferCoyoteTest
{
    [Fact]
    public async Task TryEnqueue_GivenConcurrentProducerConsumerFullQueue_ThenNeverOverwritesOrLosesData()
    {
        // Arrange
        const int capacity = 16;
        const int totalItems = 500;
        var buffer = new SpscRingBuffer<int>(capacity: capacity);
        var consumedItems = new int[totalItems];
        var consumedCount = 0;

        // Act - Run concurrent Producer and Consumer tasks
        var producer = Task.Run(() =>
        {
            for (var i = 1; i <= totalItems; i++)
            {
                while (!buffer.TryEnqueue(i))
                {
                    // Spin-wait until space becomes available
                    #if NETCOREAPP
                    Thread.SpinWait(1);
                    #endif
                }
            }
        });

        var consumer = Task.Run(() =>
        {
            var itemsDequeued = 0;
            while (itemsDequeued < totalItems)
            {
                if (buffer.TryDequeue(out var val))
                {
                    consumedItems[itemsDequeued] = val;
                    itemsDequeued++;
                }
                else
                {
                    #if NETCOREAPP
                    Thread.SpinWait(1);
                    #endif
                }
            }
            consumedCount = itemsDequeued;
        });

        await Task.WhenAll(producer, consumer);

        // Assert
        consumedCount.Should().Be(totalItems);
        for (var i = 0; i < totalItems; i++)
        {
            consumedItems[i].Should().Be(i + 1, $"Item at index {i} must match strictly sequential producer value without corruption or off-by-one overwrites");
        }
    }

    [Fact]
    public void TryEnqueue_GivenCoyoteControlledExecution_ExploresConcurrentInterleaving()
    {
        // Arrange - Configure Coyote's systematic testing engine for 100 deterministic iterations
        var config = Configuration.Create()
            .WithTestingIterations(100);

        // Act & Assert - Run the test under Coyote's controlled testing engine
        var engine = TestingEngine.Create(config, () =>
        {
            const int capacity = 8;
            const int totalItems = 100;
            var buffer = new SpscRingBuffer<long>(capacity: capacity);

            var producer = Task.Run(() =>
            {
                for (long i = 1; i <= totalItems; i++)
                {
                    while (!buffer.TryEnqueue(i))
                    {
                    }
                }
            });

            var consumer = Task.Run(() =>
            {
                long expected = 1;
                while (expected <= totalItems)
                {
                    if (buffer.TryDequeue(out var val))
                    {
                        if (val != expected)
                        {
                            throw new InvalidOperationException($"Out-of-order or corrupted read! Expected {expected}, got {val}");
                        }
                        expected++;
                    }
                }
            });

            Task.WaitAll(producer, consumer);
        });

        engine.Run();

        // Verify that Coyote found 0 concurrency/invariant bugs across all iterations
        engine.TestReport.NumOfFoundBugs.Should().Be(0, "Coyote systematic concurrency testing should find 0 race conditions or boundary corruptions");
    }
}

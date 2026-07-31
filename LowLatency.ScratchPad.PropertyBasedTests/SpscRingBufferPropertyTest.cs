using FsCheck.Xunit;
using LowLatency.ScratchPad.Engine;

namespace LowLatency.ScratchPad.PropertyBasedTests;

public class SpscRingBufferPropertyTest
{
    [Property(MaxTest = 1000)]
    public bool BitwiseMasking_GivenAnySequenceNumberAndPowerOfTwoCapacity_MatchesModuloArithmetic(byte capacityExponent, ulong sequence)
    {
        // Restrict capacity exponent between 1 and 16 (capacities 2 to 65,536)
        var exp = (capacityExponent % 16) + 1;
        var capacity = 1 << exp;
        var mask = capacity - 1;

        // Act
        var bitwiseIndex = (int)(sequence & (ulong)mask);
        var moduloIndex = (int)(sequence % (ulong)capacity);

        // Assert - Bitwise AND masking must be mathematically identical to modulo division
        return bitwiseIndex == moduloIndex;
    }

    [Property(MaxTest = 500)]
    public bool TryEnqueueDequeue_GivenRandomItems_MatchesReferenceQueueModel(byte capacityExponent, int[]? itemsToEnqueue)
    {
        // Arrange
        var exp = (capacityExponent % 8) + 1; // Capacities 2 to 256
        var capacity = 1 << exp;
        var ringBuffer = new SpscRingBuffer<int>(capacity: capacity);
        var referenceQueue = new Queue<int>();

        if (itemsToEnqueue == null)
        {
            return true;
        }

        // Act & Assert
        foreach (var item in itemsToEnqueue)
        {
            var expectedSuccess = referenceQueue.Count < capacity;
            var actualSuccess = ringBuffer.TryEnqueue(item);

            if (actualSuccess != expectedSuccess)
            {
                return false;
            }

            if (actualSuccess)
            {
                referenceQueue.Enqueue(item);
            }

            // Periodically dequeue and verify model alignment
            if (referenceQueue.Count > 0 && Math.Abs(item) % 3 == 0)
            {
                var expectedVal = referenceQueue.Dequeue();
                var dequeueSuccess = ringBuffer.TryDequeue(out var actualVal);

                if (!dequeueSuccess || actualVal != expectedVal)
                {
                    return false;
                }
            }
        }

        // Flush remaining items
        while (referenceQueue.Count > 0)
        {
            var expectedVal = referenceQueue.Dequeue();
            var dequeueSuccess = ringBuffer.TryDequeue(out var actualVal);

            if (!dequeueSuccess || actualVal != expectedVal)
            {
                return false;
            }
        }

        return true;
    }
}

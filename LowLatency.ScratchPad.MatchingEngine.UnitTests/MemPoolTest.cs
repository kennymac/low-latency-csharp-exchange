using LowLatency.ScratchPad.Engine;
using LowLatency.ScratchPad.Engine.Model;

namespace LowLatency.ScratchPad.MatchingEngine.UnitTests;

public class MemPoolTest
{
    [Fact]
    public void Deallocate_GivenPoolIsFull_ThenThrowsInvalidOperationException()
    {
        // Arrange
        var pool = new MemPool<Order>(capacity: 2);
        var order1 = pool.Allocate();

        // Act: Deallocate returned item so pool becomes completely full again
        pool.Deallocate(order1);

        // Act & Assert: Attempting to deallocate when pool is full should throw InvalidOperationException
        var deallocateExtra = () => pool.Deallocate(new Order());
        deallocateExtra.Should().Throw<InvalidOperationException>()
            .WithMessage("*overflow*");
    }
}

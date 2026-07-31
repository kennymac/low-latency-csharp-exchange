#pragma warning disable CS8602

using LowLatency.ScratchPad.IdiomaticEngine.Model;

namespace LowLatency.ScratchPad.IdiomaticEngine.UnitTests;

public class IdiomaticOrderBookTest : IClientResponseListener, IMarketUpdateListener
{
    private readonly List<ClientResponse> _responses = [];
    private readonly List<MarketUpdate> _updates = [];

    void IClientResponseListener.OnClientResponse(ClientResponse response)
    {
        _responses.Add(response);
    }

    void IMarketUpdateListener.OnMarketUpdate(MarketUpdate update)
    {
        _updates.Add(update);
    }

    [Fact]
    public void Add_GivenNonOverlappingBuyAndSellOrders_ThenBothRestInBook()
    {
        // Arrange
        var book = new IdiomaticOrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);

        // Act
        book.Add(clientId: 1, clientOrderId: 100, side: Side.Buy, price: 99, qty: 10);
        book.Add(clientId: 2, clientOrderId: 200, side: Side.Sell, price: 101, qty: 5);

        // Assert
        _responses.Should().HaveCount(2);
        _responses[0].Type.Should().Be(ClientResponseType.Accepted);
        _responses[1].Type.Should().Be(ClientResponseType.Accepted);

        _updates.Should().HaveCount(2);
        _updates[0].Type.Should().Be(MarketUpdateType.Add);
        _updates[1].Type.Should().Be(MarketUpdateType.Add);
    }

    [Fact]
    public void Add_GivenMatchingBuyAndSellOrders_ThenExecutesExactFill()
    {
        // Arrange
        var book = new IdiomaticOrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);
        book.Add(clientId: 1, clientOrderId: 100, side: Side.Sell, price: 100, qty: 10);
        _responses.Clear();
        _updates.Clear();

        // Act
        book.Add(clientId: 2, clientOrderId: 200, side: Side.Buy, price: 100, qty: 10);

        // Assert
        // Responses: 1 Taker Accepted + 1 Taker Fill + 1 Maker Fill = 3 responses
        _responses.Should().HaveCount(3);
        _responses[0].Type.Should().Be(ClientResponseType.Accepted);
        _responses[1].Type.Should().Be(ClientResponseType.Filled);
        _responses[2].Type.Should().Be(ClientResponseType.Filled);

        _updates.Should().HaveCount(1);
        _updates[0].Type.Should().Be(MarketUpdateType.Trade);
    }

    [Fact]
    public void Cancel_GivenExistingRestingOrder_ThenRemovesOrderAndEmitsEvents()
    {
        // Arrange
        var book = new IdiomaticOrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);
        book.Add(clientId: 1, clientOrderId: 100, side: Side.Buy, price: 99, qty: 10);
        _responses.Clear();
        _updates.Clear();

        // Act
        book.Cancel(clientId: 1, clientOrderId: 100);

        // Assert
        _responses.Should().HaveCount(1);
        _responses[0].Type.Should().Be(ClientResponseType.Canceled);

        _updates.Should().HaveCount(1);
        _updates[0].Type.Should().Be(MarketUpdateType.Cancel);
    }

    [Fact]
    public void Add_GivenMatchingOrders_WhenAssertingZeroAllocations_ThenFailsForIdiomaticEngine()
    {
        // Arrange
        var book = new IdiomaticOrderBook(tickerId: 1, clientResponseListener: null, marketUpdateListener: null);

        // Warmup JIT & initial allocations
        book.Add(clientId: 1, clientOrderId: 1, side: Side.Sell, price: 100, qty: 10);
        book.Add(clientId: 2, clientOrderId: 2, side: Side.Buy, price: 100, qty: 10);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();

        // Act - Match 10 pairs of orders in standard idiomatic C#
        for (ulong i = 10; i < 20; i++)
        {
            book.Add(clientId: 1, clientOrderId: i, side: Side.Sell, price: 100, qty: 10);
            book.Add(clientId: 2, clientOrderId: i + 100, side: Side.Buy, price: 100, qty: 10);
        }

        var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        var bytesAllocated = bytesAfter - bytesBefore;

        // VERIFICATION: Unlike the Low-Latency Engine (which allocates 0 Bytes),
        // Idiomatic C# allocates memory on every order match due to `new Order()`, `LinkedListNode<T>`, etc.
        bytesAllocated.Should().BeGreaterThan(0, "Idiomatic C# allocates heap objects per matched order pair.");

        // Note: Demonstrates why asserting zero-allocations would fail for the idiomatic engine:
        // Action assertZeroAlloc = () => bytesAllocated.Should().Be(0);
        // assertZeroAlloc.Should().Throw<XunitException>();
    }
}

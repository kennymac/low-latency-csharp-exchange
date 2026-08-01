using LowLatency.ScratchPad.Engine;
using LowLatency.ScratchPad.Engine.Model;

namespace LowLatency.ScratchPad.MatchingEngine.UnitTests;

public class OrderBookLimitConditionsTest : IClientResponseListener, IMarketUpdateListener
{
    private readonly List<ClientResponse> _responses = [];
    private readonly List<MarketUpdate> _updates = [];

    void IClientResponseListener.OnClientResponse(in ClientResponse response)
    {
        _responses.Add(response);
    }

    void IMarketUpdateListener.OnMarketUpdate(in MarketUpdate update)
    {
        _updates.Add(update);
    }

    [Fact]
    public void LimitCondition_ExceedingMaxOrders_RejectsOrderGracefullyWithoutCrashing()
    {
        // Arrange: Create book with small capacity (e.g. MaxOrders limit)
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);

        // Fill order pool to capacity (MaxOrders = 10,000)
        for (var i = 1uL; i <= OrderBook.MaxOrders; i++)
        {
            book.Add(clientId: 1, clientOrderId: i, side: Side.Sell, price: 100, qty: 10);
        }

        _responses.Clear();

        // Act: Attempt to add (MaxOrders + 1)-th order
        var act = () => book.Add(clientId: 1, clientOrderId: OrderBook.MaxOrders + 1, side: Side.Sell, price: 100, qty: 10);

        // Assert: System must NOT crash with unhandled exception, and order must be rejected gracefully
        act.Should().NotThrow();
        _responses.Should().HaveCount(1);
        _responses[0].Type.Should().Be(ClientResponseType.Rejected);
        _responses[0].ClientOrderId.Should().Be(OrderBook.MaxOrders + 1);
    }

    [Fact]
    public void LimitCondition_ExceedingMaxPriceLevels_RejectsOrderGracefullyWithoutCrashing()
    {
        // Arrange: Book with MaxPriceLevels = 1,000
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);

        // Fill price levels pool to capacity (1,000 distinct price levels)
        for (var i = 1; i <= OrderBook.MaxPriceLevels; i++)
        {
            book.Add(clientId: 1, clientOrderId: (ulong)i, side: Side.Sell, price: i * 2, qty: 10);
        }

        _responses.Clear();

        // Act: Attempt to add 1,001-st distinct price level
        var act = () => book.Add(clientId: 1, clientOrderId: 99999, side: Side.Sell, price: 2004, qty: 10);

        // Assert: System must NOT crash, order must be rejected gracefully
        act.Should().NotThrow();
        _responses.Should().HaveCount(1);
        _responses[0].Type.Should().Be(ClientResponseType.Rejected);
        _responses[0].ClientOrderId.Should().Be(99999);
    }

    [Fact]
    public void LimitCondition_ClientIdExceedsMaxClients_RejectsOrderGracefully()
    {
        // Arrange: ClientId >= MaxClients (MaxClients = 100)
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);

        uint invalidClientId = (uint)OrderBook.MaxClients + 1; // Client ID 101

        // Act
        var act = () => book.Add(clientId: invalidClientId, clientOrderId: 1, side: Side.Buy, price: 100, qty: 10);

        // Assert
        act.Should().NotThrow();
        _responses.Should().HaveCount(1);
        _responses[0].Type.Should().Be(ClientResponseType.Rejected);
        _responses[0].ClientId.Should().Be(invalidClientId);
    }

    [Fact]
    public void LimitCondition_SingleClientExceedsMaxOrdersPerClient_RejectsOrderGracefully()
    {
        // Arrange: MaxOrdersPerClient = 1,000
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);

        // Fill Client 1's order quota (1,000 active resting orders)
        for (var i = 1uL; i <= OrderBook.MaxOrdersPerClient; i++)
        {
            book.Add(clientId: 1, clientOrderId: i, side: Side.Buy, price: 100, qty: 10);
        }

        _responses.Clear();

        // Act: Client 1 attempts 1,001-st active order
        var act = () => book.Add(clientId: 1, clientOrderId: 1001, side: Side.Buy, price: 101, qty: 10);

        // Assert
        act.Should().NotThrow();
        _responses.Should().HaveCount(1);
        _responses[0].Type.Should().Be(ClientResponseType.Rejected);
        _responses[0].ClientOrderId.Should().Be(1001);
    }
}

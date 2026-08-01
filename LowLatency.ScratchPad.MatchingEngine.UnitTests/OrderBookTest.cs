#pragma warning disable CS8602

using LowLatency.ScratchPad.Engine;
using LowLatency.ScratchPad.Engine.Model;

namespace LowLatency.ScratchPad.MatchingEngine.UnitTests;

public class OrderBookTest : IClientResponseListener, IMarketUpdateListener
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
    public void Add_GivenNonOverlappingBuyAndSellOrders_ThenBothRestInBook()
    {
        // Arrange
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);

        // Act
        book.Add(clientId: 1, clientOrderId: 100, side: Side.Buy, price: 99, qty: 10);
        book.Add(clientId: 2, clientOrderId: 200, side: Side.Sell, price: 101, qty: 5);

        // Assert
        book.BidsByPrice.Should().NotBeNull();
        book.BidsByPrice.Price.Should().Be(99);
        book.BidsByPrice.FirstOrder.Qty.Should().Be(10);

        book.AsksByPrice.Should().NotBeNull();
        book.AsksByPrice.Price.Should().Be(101);
        book.AsksByPrice.FirstOrder.Qty.Should().Be(5);

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
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);
        book.Add(clientId: 1, clientOrderId: 100, side: Side.Sell, price: 100, qty: 10);
        _responses.Clear();
        _updates.Clear();

        // Act
        book.Add(clientId: 2, clientOrderId: 200, side: Side.Buy, price: 100, qty: 10);

        // Assert
        book.AsksByPrice.Should().BeNull();

        _responses.Should().HaveCount(3);
        _responses[0].Type.Should().Be(ClientResponseType.Accepted);
        _responses[0].ClientId.Should().Be(2);

        _responses[1].Type.Should().Be(ClientResponseType.Filled);
        _responses[1].ClientId.Should().Be(2);
        _responses[1].ExecQty.Should().Be(10);
        _responses[1].LeavesQty.Should().Be(0);

        _responses[2].Type.Should().Be(ClientResponseType.Filled);
        _responses[2].ClientId.Should().Be(1);
        _responses[2].ExecQty.Should().Be(10);
        _responses[2].LeavesQty.Should().Be(0);

        _updates.Should().HaveCount(2);
        _updates[0].Type.Should().Be(MarketUpdateType.Trade);
        _updates[0].Qty.Should().Be(10);
        _updates[1].Type.Should().Be(MarketUpdateType.Cancel);
    }

    [Fact]
    public void Add_GivenTakerOrderLargerThanMaker_ThenPartiallyFillsMakerAndRestsRemainder()
    {
        // Arrange
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);
        book.Add(clientId: 1, clientOrderId: 100, side: Side.Sell, price: 100, qty: 5);
        _responses.Clear();
        _updates.Clear();

        // Act
        book.Add(clientId: 2, clientOrderId: 200, side: Side.Buy, price: 100, qty: 12);

        // Assert
        book.AsksByPrice.Should().BeNull();
        book.BidsByPrice.Should().NotBeNull();
        book.BidsByPrice.Price.Should().Be(100);
        book.BidsByPrice.FirstOrder.Qty.Should().Be(7);

        _responses.Should().HaveCount(3);
        _responses[1].ExecQty.Should().Be(5);
        _responses[1].LeavesQty.Should().Be(7);

        _responses[2].ExecQty.Should().Be(5);
        _responses[2].LeavesQty.Should().Be(0);
    }

    [Fact]
    public void Add_GivenMultipleOrdersAtSamePrice_ThenExecutesInFifoSequence()
    {
        // Arrange
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);
        book.Add(clientId: 1, clientOrderId: 101, side: Side.Sell, price: 100, qty: 5);
        book.Add(clientId: 2, clientOrderId: 102, side: Side.Sell, price: 100, qty: 5);
        _responses.Clear();
        _updates.Clear();

        // Act
        book.Add(clientId: 3, clientOrderId: 200, side: Side.Buy, price: 100, qty: 7);

        // Assert
        book.AsksByPrice.Should().NotBeNull();
        book.AsksByPrice.FirstOrder.ClientId.Should().Be(2);
        book.AsksByPrice.FirstOrder.Qty.Should().Be(3);

        var makerFills = _responses.Where(r => r.Type == ClientResponseType.Filled && r.ClientId != 3).ToList();
        makerFills.Should().HaveCount(2);

        makerFills[0].ClientId.Should().Be(1);
        makerFills[0].ExecQty.Should().Be(5);

        makerFills[1].ClientId.Should().Be(2);
        makerFills[1].ExecQty.Should().Be(2);
        makerFills[1].LeavesQty.Should().Be(3);
    }

    [Fact]
    public void Add_GivenTakerOrderSweepingMultiplePrices_ThenMatchesBestPriceFirst()
    {
        // Arrange
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);
        book.Add(clientId: 1, clientOrderId: 101, side: Side.Sell, price: 100, qty: 5);
        book.Add(clientId: 2, clientOrderId: 102, side: Side.Sell, price: 101, qty: 5);
        _responses.Clear();

        // Act
        book.Add(clientId: 3, clientOrderId: 200, side: Side.Buy, price: 105, qty: 8);

        // Assert
        var makerFills = _responses.Where(r => r.Type == ClientResponseType.Filled && r.ClientId != 3).ToList();
        makerFills.Should().HaveCount(2);

        makerFills[0].ClientId.Should().Be(1);
        makerFills[0].Price.Should().Be(100);
        makerFills[0].ExecQty.Should().Be(5);

        makerFills[1].ClientId.Should().Be(2);
        makerFills[1].Price.Should().Be(101);
        makerFills[1].ExecQty.Should().Be(3);
        makerFills[1].LeavesQty.Should().Be(2);

        book.AsksByPrice.Should().NotBeNull();
        book.AsksByPrice.Price.Should().Be(101);
        book.AsksByPrice.FirstOrder.Qty.Should().Be(2);
    }

    [Fact]
    public void Cancel_GivenExistingRestingOrder_ThenRemovesOrderAndEmitsCanceled()
    {
        // Arrange
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);
        book.Add(clientId: 1, clientOrderId: 100, side: Side.Buy, price: 99, qty: 10);
        _responses.Clear();
        _updates.Clear();

        // Act
        book.Cancel(clientId: 1, clientOrderId: 100);

        // Assert
        book.BidsByPrice.Should().BeNull();

        _responses.Should().HaveCount(1);
        _responses[0].Type.Should().Be(ClientResponseType.Canceled);
        _responses[0].ClientId.Should().Be(1);
        _responses[0].ClientOrderId.Should().Be(100);

        _updates.Should().HaveCount(1);
        _updates[0].Type.Should().Be(MarketUpdateType.Cancel);
    }

    [Fact]
    public void Cancel_GivenNonExistentOrder_ThenEmitsCancelRejected()
    {
        // Arrange
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);
        _responses.Clear();

        // Act
        book.Cancel(clientId: 99, clientOrderId: 999);

        // Assert
        _responses.Should().HaveCount(1);
        _responses[0].Type.Should().Be(ClientResponseType.CancelRejected);
        _responses[0].ClientId.Should().Be(99);
        _responses[0].ClientOrderId.Should().Be(999);
    }

    [Fact]
    public void Add_GivenBulkOrdersFlow_ThenZeroGcAllocations()
    {
        // Arrange
        var book = new OrderBook(tickerId: 1, clientResponseListener: null, marketUpdateListener: null);

        // Warm-up phase: run cycle to JIT compile methods and warm up internal memory pools
        for (var i = 1uL; i <= 50uL; i++)
        {
            book.Add(clientId: 1, clientOrderId: i, side: Side.Sell, price: 100 + (long)(i % 10), qty: 10);
            book.Add(clientId: 2, clientOrderId: i, side: Side.Buy, price: 100 + (long)(i % 10), qty: 10);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();

        // Act: Process 10,000 orders (sustained bulk Adds, Matches, and Cancels)
        for (var i = 100uL; i < 5_100uL; i++)
        {
            var client1 = (uint)(i % 50) + 1;
            var client2 = (uint)((i + 1) % 50) + 1;

            book.Add(clientId: client1, clientOrderId: i, side: Side.Sell, price: 200 + (long)(i % 20), qty: 5);
            book.Add(clientId: client2, clientOrderId: i + 10_000, side: Side.Buy, price: 200 + (long)(i % 20), qty: 5);
        }

        var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        var totalAllocatedBytes = bytesAfter - bytesBefore;

        // Assert zero bytes allocated on the managed heap
        totalAllocatedBytes.Should().Be(0, "no bytes should be allocated to the managed heap");
    }
}

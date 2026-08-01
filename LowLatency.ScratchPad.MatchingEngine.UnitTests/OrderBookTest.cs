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

    [Fact]
    public void Add_GivenPricesCollidingUnderModulo_ThenRestInDistinctPriceLevels()
    {
        // Arrange
        // MaxPriceLevels = 1000. Price 150 and Price 1150 collide under price % 1000.
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);

        // Act
        book.Add(clientId: 1, clientOrderId: 1, side: Side.Sell, price: 150, qty: 10);
        book.Add(clientId: 2, clientOrderId: 2, side: Side.Sell, price: 1150, qty: 5);

        _responses.Clear();
        _updates.Clear();

        // Taker buy order at 150 should match the maker order at 150 (best ask price)
        book.Add(clientId: 3, clientOrderId: 3, side: Side.Buy, price: 150, qty: 10);

        // Assert
        var makerFills = _responses.Where(r => r.Type == ClientResponseType.Filled && r.ClientId != 3).ToList();
        makerFills.Should().HaveCount(1);
        makerFills[0].ClientId.Should().Be(1);
        makerFills[0].Price.Should().Be(150);
        makerFills[0].ExecQty.Should().Be(10);

        // The order at price 1150 must still exist untouched with full quantity 5
        book.AsksByPrice.Should().NotBeNull();
        book.AsksByPrice.Price.Should().Be(1150);
        book.AsksByPrice.FirstOrder.Qty.Should().Be(5);
        book.AsksByPrice.FirstOrder.ClientId.Should().Be(2);
    }


    [Fact]
    public void Cancel_GivenSequentialOrderIdsCollidingUnderModulo_ThenBothOrdersCanBeCancelledIndividually()
    {
        // Arrange
        // MaxOrdersPerClient = 1000. ClientOrderId 1 and ClientOrderId 1001 map to index 1 under % 1000.
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);

        book.Add(clientId: 1, clientOrderId: 1, side: Side.Buy, price: 100, qty: 10);
        book.Add(clientId: 1, clientOrderId: 1001, side: Side.Buy, price: 102, qty: 10);

        _responses.Clear();

        // Act: Cancel order 1
        book.Cancel(clientId: 1, clientOrderId: 1);

        // Assert: Order 1 should be cancelled successfully (NOT CancelRejected)
        _responses.Should().HaveCount(1);
        _responses[0].Type.Should().Be(ClientResponseType.Canceled);
        _responses[0].ClientOrderId.Should().Be(1);

        // Order 1001 should still be accessible and resting in book
        var order1001 = book.GetOrder(clientId: 1, clientOrderId: 1001);
        order1001.Should().NotBeNull();
        order1001.Price.Should().Be(102);
    }

    [Fact]
    public void Add_GivenWideMarketPriceSpreads_ThenOrdersRestAtCorrectPriceLevels()
    {
        // Arrange: Real market prices spanning small, medium, large, and 64-bit values
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);

        long[] prices = [10, 500, 1050, 50_000, 1_000_000, 99_999_999];

        // Act: Place orders at wide price variance
        for (var i = 0; i < prices.Length; i++)
        {
            book.Add(clientId: 1, clientOrderId: (ulong)(i + 1), side: Side.Sell, price: prices[i], qty: 10);
        }

        // Assert: All price levels exist independently and orders are retrievable
        for (var i = 0; i < prices.Length; i++)
        {
            var order = book.GetOrder(clientId: 1, clientOrderId: (ulong)(i + 1));
            order.Should().NotBeNull();
            order.Price.Should().Be(prices[i]);
        }
    }

    [Fact]
    public void Add_GivenHighClientOrderIdsAndMultipleClients_ThenAllOrdersRetrievableAndCancellable()
    {
        // Arrange: Realistic order IDs generated by clients (e.g., Unix timestamp milliseconds + counter)
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);

        ulong orderId1 = 1_700_000_000_001UL;
        ulong orderId2 = 1_700_000_001_001UL; // Differs by 1000 from orderId1
        ulong orderId3 = 9_876_543_210_999UL;

        // Act
        book.Add(clientId: 5, clientOrderId: orderId1, side: Side.Buy, price: 100, qty: 10);
        book.Add(clientId: 5, clientOrderId: orderId2, side: Side.Buy, price: 101, qty: 10);
        book.Add(clientId: 12, clientOrderId: orderId3, side: Side.Sell, price: 105, qty: 5);

        // Assert
        book.GetOrder(clientId: 5, clientOrderId: orderId1).Should().NotBeNull();
        book.GetOrder(clientId: 5, clientOrderId: orderId2).Should().NotBeNull();
        book.GetOrder(clientId: 12, clientOrderId: orderId3).Should().NotBeNull();

        _responses.Clear();
        book.Cancel(clientId: 5, clientOrderId: orderId1);

        _responses.Should().HaveCount(1);
        _responses[0].Type.Should().Be(ClientResponseType.Canceled);
        _responses[0].ClientOrderId.Should().Be(orderId1);
    }

    [Fact]
    public void Add_GivenPriceExceedsMaxSupportedPrice_ThenOrderIsRejectedAndDoesNotCauseError()
    {
        // Arrange: Price exceeds maximum supported bounds (e.g. > MaxSupportedPrice or <= 0)
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);

        long invalidPrice = OrderBook.MaxSupportedPrice + 1;

        // Act & Assert: Order should be gracefully rejected without throwing exception
        var act = () => book.Add(clientId: 1, clientOrderId: 99, side: Side.Buy, price: invalidPrice, qty: 10);
        act.Should().NotThrow();

        // Client response should indicate rejection
        _responses.Should().HaveCount(1);
        _responses[0].Type.Should().Be(ClientResponseType.Rejected);
        _responses[0].ClientOrderId.Should().Be(99);

        // Order book state should remain clean and uncorrupted
        book.BidsByPrice.Should().BeNull();
        book.GetOrder(clientId: 1, clientOrderId: 99).Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Add_GivenZeroOrNegativePrice_ThenOrderIsRejectedAndDoesNotCauseError(long invalidPrice)
    {
        // Arrange
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: this);

        // Act & Assert: Order should be gracefully rejected without throwing exception
        var act = () => book.Add(clientId: 1, clientOrderId: 99, side: Side.Buy, price: invalidPrice, qty: 10);
        act.Should().NotThrow();

        // Client response should indicate rejection
        _responses.Should().HaveCount(1);
        _responses[0].Type.Should().Be(ClientResponseType.Rejected);
        _responses[0].ClientOrderId.Should().Be(99);

        // Order book state should remain clean and uncorrupted
        book.BidsByPrice.Should().BeNull();
        book.GetOrder(clientId: 1, clientOrderId: 99).Should().BeNull();
    }
}





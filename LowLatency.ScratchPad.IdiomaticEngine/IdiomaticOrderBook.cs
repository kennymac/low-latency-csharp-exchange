using LowLatency.ScratchPad.IdiomaticEngine.Model;

namespace LowLatency.ScratchPad.IdiomaticEngine;

public class DescendingComparer : IComparer<long>
{
    public int Compare(long x, long y) => y.CompareTo(x);
}

public sealed class IdiomaticOrderBook
{
    public uint TickerId { get; }

    private readonly IClientResponseListener? _clientResponseListener;
    private readonly IMarketUpdateListener? _marketUpdateListener;

    // Bids: Sorted high to low price
    private readonly SortedDictionary<long, LinkedList<Order>> _bids = new(new DescendingComparer());

    // Asks: Sorted low to high price
    private readonly SortedDictionary<long, LinkedList<Order>> _asks = new();

    // Map for O(1) cancel lookup by (clientId, clientOrderId)
    private readonly Dictionary<(uint clientId, ulong clientOrderId), LinkedListNode<Order>> _orderMap = new();

    private ulong _nextMarketOrderId = 1;
    private ulong _priorityCounter = 1;

    public IdiomaticOrderBook(
        uint tickerId,
        IClientResponseListener? clientResponseListener = null,
        IMarketUpdateListener? marketUpdateListener = null)
    {
        TickerId = tickerId;
        _clientResponseListener = clientResponseListener;
        _marketUpdateListener = marketUpdateListener;
    }

    public void Add(
        uint clientId,
        ulong clientOrderId,
        Side side,
        long price,
        uint qty)
    {
        var marketOrderId = _nextMarketOrderId++;

        _clientResponseListener?.OnClientResponse(new ClientResponse(
            type: ClientResponseType.Accepted,
            clientId: clientId,
            tickerId: TickerId,
            clientOrderId: clientOrderId,
            marketOrderId: marketOrderId,
            side: side,
            price: price,
            execQty: 0,
            leavesQty: qty));

        var leavesQty = CheckForMatch(
            clientId: clientId,
            clientOrderId: clientOrderId,
            side: side,
            price: price,
            qty: qty,
            marketOrderId: marketOrderId);

        if (leavesQty > 0)
        {
            var priority = _priorityCounter++;

            var order = new Order
            {
                TickerId = TickerId,
                ClientId = clientId,
                ClientOrderId = clientOrderId,
                MarketOrderId = marketOrderId,
                Side = side,
                Price = price,
                Qty = leavesQty,
                Priority = priority
            };

            var book = side == Side.Buy ? _bids : _asks;
            if (!book.TryGetValue(price, out var levelList))
            {
                levelList = new LinkedList<Order>();
                book[price] = levelList;
            }

            var node = levelList.AddLast(order);
            _orderMap[(clientId, clientOrderId)] = node;

            _marketUpdateListener?.OnMarketUpdate(new MarketUpdate(
                type: MarketUpdateType.Add,
                marketOrderId: marketOrderId,
                tickerId: TickerId,
                side: side,
                price: price,
                qty: leavesQty,
                priority: priority));
        }
    }

    public void Cancel(
        uint clientId,
        ulong clientOrderId)
    {
        if (!_orderMap.TryGetValue((clientId, clientOrderId), out var node))
        {
            _clientResponseListener?.OnClientResponse(new ClientResponse(
                type: ClientResponseType.CancelRejected,
                clientId: clientId,
                tickerId: TickerId,
                clientOrderId: clientOrderId,
                marketOrderId: 0,
                side: Side.Invalid,
                price: 0,
                execQty: 0,
                leavesQty: 0));
            return;
        }

        var order = node.Value;

        _clientResponseListener?.OnClientResponse(new ClientResponse(
            type: ClientResponseType.Canceled,
            clientId: clientId,
            tickerId: TickerId,
            clientOrderId: clientOrderId,
            marketOrderId: order.MarketOrderId,
            side: order.Side,
            price: order.Price,
            execQty: 0,
            leavesQty: order.Qty));

        _marketUpdateListener?.OnMarketUpdate(new MarketUpdate(
            type: MarketUpdateType.Cancel,
            marketOrderId: order.MarketOrderId,
            tickerId: TickerId,
            side: order.Side,
            price: order.Price,
            qty: 0,
            priority: order.Priority));

        RemoveOrderNode((clientId, clientOrderId), node, order.Side, order.Price);
    }

    private void RemoveOrderNode((uint, ulong) key, LinkedListNode<Order> node, Side side, long price)
    {
        _orderMap.Remove(key);
        var book = side == Side.Buy ? _bids : _asks;
        if (book.TryGetValue(price, out var levelList))
        {
            levelList.Remove(node);
            if (levelList.Count == 0)
            {
                book.Remove(price);
            }
        }
    }

    private uint CheckForMatch(
        uint clientId,
        ulong clientOrderId,
        Side side,
        long price,
        uint qty,
        ulong marketOrderId)
    {
        var leavesQty = qty;

        if (side == Side.Buy)
        {
            while (leavesQty > 0 && _asks.Count > 0)
            {
                var bestAskLevel = _asks.First();
                var askPrice = bestAskLevel.Key;
                if (price < askPrice)
                {
                    break;
                }

                var levelList = bestAskLevel.Value;
                var askNode = levelList.First!;

                Match(
                    takerClientId: clientId,
                    takerClientOrderId: clientOrderId,
                    takerSide: side,
                    takerMarketOrderId: marketOrderId,
                    makerNode: askNode,
                    levelList: levelList,
                    book: _asks,
                    makerPrice: askPrice,
                    leavesQty: ref leavesQty);
            }
        }
        else if (side == Side.Sell)
        {
            while (leavesQty > 0 && _bids.Count > 0)
            {
                var bestBidLevel = _bids.First();
                var bidPrice = bestBidLevel.Key;
                if (price > bidPrice)
                {
                    break;
                }

                var levelList = bestBidLevel.Value;
                var bidNode = levelList.First!;

                Match(
                    takerClientId: clientId,
                    takerClientOrderId: clientOrderId,
                    takerSide: side,
                    takerMarketOrderId: marketOrderId,
                    makerNode: bidNode,
                    levelList: levelList,
                    book: _bids,
                    makerPrice: bidPrice,
                    leavesQty: ref leavesQty);
            }
        }

        return leavesQty;
    }

    private void Match(
        uint takerClientId,
        ulong takerClientOrderId,
        Side takerSide,
        ulong takerMarketOrderId,
        LinkedListNode<Order> makerNode,
        LinkedList<Order> levelList,
        SortedDictionary<long, LinkedList<Order>> book,
        long makerPrice,
        ref uint leavesQty)
    {
        var makerOrder = makerNode.Value;
        var execQty = Math.Min(leavesQty, makerOrder.Qty);

        leavesQty -= execQty;
        makerOrder.Qty -= execQty;

        // Taker response
        _clientResponseListener?.OnClientResponse(new ClientResponse(
            type: ClientResponseType.Filled,
            clientId: takerClientId,
            tickerId: TickerId,
            clientOrderId: takerClientOrderId,
            marketOrderId: takerMarketOrderId,
            side: takerSide,
            price: makerPrice,
            execQty: execQty,
            leavesQty: leavesQty));

        // Maker response
        _clientResponseListener?.OnClientResponse(new ClientResponse(
            type: ClientResponseType.Filled,
            clientId: makerOrder.ClientId,
            tickerId: TickerId,
            clientOrderId: makerOrder.ClientOrderId,
            marketOrderId: makerOrder.MarketOrderId,
            side: makerOrder.Side,
            price: makerPrice,
            execQty: execQty,
            leavesQty: makerOrder.Qty));

        // Market update for trade
        _marketUpdateListener?.OnMarketUpdate(new MarketUpdate(
            type: MarketUpdateType.Trade,
            marketOrderId: makerOrder.MarketOrderId,
            tickerId: TickerId,
            side: makerOrder.Side,
            price: makerPrice,
            qty: execQty,
            priority: makerOrder.Priority));

        // If maker order completely filled, remove it
        if (makerOrder.Qty == 0)
        {
            _orderMap.Remove((makerOrder.ClientId, makerOrder.ClientOrderId));
            levelList.Remove(makerNode);
            if (levelList.Count == 0)
            {
                book.Remove(makerPrice);
            }
        }
    }
}

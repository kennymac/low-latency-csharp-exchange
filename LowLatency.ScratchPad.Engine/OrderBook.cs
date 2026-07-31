using LowLatency.ScratchPad.Engine.Model;

namespace LowLatency.ScratchPad.Engine;

public sealed class OrderBook
{
    public const int MaxOrders = 10_000;
    public const int MaxPriceLevels = 1_000;
    public const int MaxClients = 100;
    public const int MaxOrdersPerClient = 1_000;

    public uint TickerId { get; }

    private readonly IClientResponseListener? _clientResponseListener;
    private readonly IMarketUpdateListener? _marketUpdateListener;

    private readonly MemPool<Order> _orderPool;
    private readonly MemPool<OrdersAtPrice> _ordersAtPricePool;

    private OrdersAtPrice? _bidsByPrice;
    private OrdersAtPrice? _asksByPrice;

    private readonly OrdersAtPrice?[] _priceOrdersAtPrice;
    private readonly Order?[] _clientOrderMap;

    private ulong _nextMarketOrderId = 1;

    public OrdersAtPrice? BidsByPrice => _bidsByPrice;
    public OrdersAtPrice? AsksByPrice => _asksByPrice;

    public OrderBook(
        uint tickerId,
        IClientResponseListener? clientResponseListener = null,
        IMarketUpdateListener? marketUpdateListener = null)
    {
        TickerId = tickerId;
        _clientResponseListener = clientResponseListener;
        _marketUpdateListener = marketUpdateListener;

        _orderPool = new MemPool<Order>(MaxOrders);
        _ordersAtPricePool = new MemPool<OrdersAtPrice>(MaxPriceLevels);

        _priceOrdersAtPrice = new OrdersAtPrice?[MaxPriceLevels];
        _clientOrderMap = new Order?[MaxClients * MaxOrdersPerClient];
    }

    private int PriceToIndex(long price)
    {
        return (int)(Math.Abs(price) % MaxPriceLevels);
    }

    private int ClientOrderToIndex(uint clientId, ulong clientOrderId)
    {
        return (int)((clientId % MaxClients) * MaxOrdersPerClient + (clientOrderId % MaxOrdersPerClient));
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
            var priority = GetNextPriority(price);
            
            var order = _orderPool.Allocate();
            order.TickerId = TickerId;
            order.ClientId = clientId;
            order.ClientOrderId = clientOrderId;
            order.MarketOrderId = marketOrderId;
            order.Side = side;
            order.Price = price;
            order.Qty = leavesQty;
            order.Priority = priority;
            order.PrevOrder = null;
            order.NextOrder = null;

            AddOrder(order);

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
        var index = ClientOrderToIndex(clientId: clientId, clientOrderId: clientOrderId);
        var order = _clientOrderMap[index];

        if (order == null || order.ClientId != clientId || order.ClientOrderId != clientOrderId)
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

        RemoveOrder(order);
    }

    public Order? GetOrder(
        uint clientId,
        ulong clientOrderId)
    {
        var index = ClientOrderToIndex(clientId: clientId, clientOrderId: clientOrderId);
        var order = _clientOrderMap[index];
        if (order != null && order.ClientId == clientId && order.ClientOrderId == clientOrderId)
        {
            return order;
        }
        return null;
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
            while (leavesQty > 0 && _asksByPrice != null)
            {
                var askItr = _asksByPrice.FirstOrder!;
                if (price < askItr.Price)
                {
                    break;
                }

                Match(
                    takerClientId: clientId,
                    takerClientOrderId: clientOrderId,
                    takerSide: side,
                    takerMarketOrderId: marketOrderId,
                    makerOrder: askItr,
                    leavesQty: ref leavesQty);
            }
        }
        else if (side == Side.Sell)
        {
            while (leavesQty > 0 && _bidsByPrice != null)
            {
                var bidItr = _bidsByPrice.FirstOrder!;
                if (price > bidItr.Price)
                {
                    break;
                }

                Match(
                    takerClientId: clientId,
                    takerClientOrderId: clientOrderId,
                    takerSide: side,
                    takerMarketOrderId: marketOrderId,
                    makerOrder: bidItr,
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
        Order makerOrder,
        ref uint leavesQty)
    {
        var makerOrderQty = makerOrder.Qty;
        var fillQty = Math.Min(leavesQty, makerOrderQty);

        leavesQty -= fillQty;
        makerOrder.Qty -= fillQty;

        // Response to Taker
        _clientResponseListener?.OnClientResponse(new ClientResponse(
            type: ClientResponseType.Filled,
            clientId: takerClientId,
            tickerId: TickerId,
            clientOrderId: takerClientOrderId,
            marketOrderId: takerMarketOrderId,
            side: takerSide,
            price: makerOrder.Price,
            execQty: fillQty,
            leavesQty: leavesQty));

        // Response to Maker
        _clientResponseListener?.OnClientResponse(new ClientResponse(
            type: ClientResponseType.Filled,
            clientId: makerOrder.ClientId,
            tickerId: TickerId,
            clientOrderId: makerOrder.ClientOrderId,
            marketOrderId: makerOrder.MarketOrderId,
            side: makerOrder.Side,
            price: makerOrder.Price,
            execQty: fillQty,
            leavesQty: makerOrder.Qty));

        // Trade Market Update
        _marketUpdateListener?.OnMarketUpdate(new MarketUpdate(
            type: MarketUpdateType.Trade,
            marketOrderId: 0,
            tickerId: TickerId,
            side: takerSide,
            price: makerOrder.Price,
            qty: fillQty,
            priority: 0));

        if (makerOrder.Qty == 0)
        {
            _marketUpdateListener?.OnMarketUpdate(new MarketUpdate(
                type: MarketUpdateType.Cancel,
                marketOrderId: makerOrder.MarketOrderId,
                tickerId: TickerId,
                side: makerOrder.Side,
                price: makerOrder.Price,
                qty: makerOrderQty,
                priority: 0));

            RemoveOrder(makerOrder);
        }
        else
        {
            _marketUpdateListener?.OnMarketUpdate(new MarketUpdate(
                type: MarketUpdateType.Modify,
                marketOrderId: makerOrder.MarketOrderId,
                tickerId: TickerId,
                side: makerOrder.Side,
                price: makerOrder.Price,
                qty: makerOrder.Qty,
                priority: makerOrder.Priority));
        }
    }

    private ulong GetNextPriority(long price)
    {
        var ordersAtPrice = _priceOrdersAtPrice[PriceToIndex(price)];
        if (ordersAtPrice != null && ordersAtPrice.FirstOrder != null)
        {
            return ordersAtPrice.FirstOrder.PrevOrder!.Priority + 1;
        }
        return 1;
    }

    private void AddOrder(Order order)
    {
        var priceIdx = PriceToIndex(order.Price);
        var ordersAtPrice = _priceOrdersAtPrice[priceIdx];

        if (ordersAtPrice == null)
        {
            order.NextOrder = order;
            order.PrevOrder = order;

            ordersAtPrice = _ordersAtPricePool.Allocate();
            ordersAtPrice.Side = order.Side;
            ordersAtPrice.Price = order.Price;
            ordersAtPrice.FirstOrder = order;
            ordersAtPrice.PrevEntry = null;
            ordersAtPrice.NextEntry = null;

            AddOrdersAtPrice(ordersAtPrice);
        }
        else
        {
            var firstOrder = ordersAtPrice.FirstOrder!;
            var lastOrder = firstOrder.PrevOrder!;

            lastOrder.NextOrder = order;
            order.PrevOrder = lastOrder;
            order.NextOrder = firstOrder;
            firstOrder.PrevOrder = order;
        }

        _clientOrderMap[ClientOrderToIndex(clientId: order.ClientId, clientOrderId: order.ClientOrderId)] = order;
    }

    private void RemoveOrder(Order order)
    {
        var priceIdx = PriceToIndex(order.Price);
        var ordersAtPrice = _priceOrdersAtPrice[priceIdx]!;

        if (order.PrevOrder == order)
        {
            RemoveOrdersAtPrice(side: order.Side, price: order.Price);
        }
        else
        {
            var orderBefore = order.PrevOrder!;
            var orderAfter = order.NextOrder!;
            orderBefore.NextOrder = orderAfter;
            orderAfter.PrevOrder = orderBefore;

            if (ordersAtPrice.FirstOrder == order)
            {
                ordersAtPrice.FirstOrder = orderAfter;
            }

            order.PrevOrder = null;
            order.NextOrder = null;
        }

        _clientOrderMap[ClientOrderToIndex(clientId: order.ClientId, clientOrderId: order.ClientOrderId)] = null;
        // _orderPool.Deallocate(order);
    }

    private void AddOrdersAtPrice(OrdersAtPrice newOrdersAtPrice)
    {
        _priceOrdersAtPrice[PriceToIndex(newOrdersAtPrice.Price)] = newOrdersAtPrice;

        ref var bestOrdersByPrice = ref (newOrdersAtPrice.Side == Side.Buy ? ref _bidsByPrice : ref _asksByPrice);

        if (bestOrdersByPrice == null)
        {
            bestOrdersByPrice = newOrdersAtPrice;
            newOrdersAtPrice.PrevEntry = newOrdersAtPrice;
            newOrdersAtPrice.NextEntry = newOrdersAtPrice;
            return;
        }

        var target = bestOrdersByPrice;
        bool addAfter;

        if (newOrdersAtPrice.Side == Side.Buy)
        {
            addAfter = newOrdersAtPrice.Price < target.Price;
        }
        else
        {
            addAfter = newOrdersAtPrice.Price > target.Price;
        }

        while (addAfter && target.NextEntry != bestOrdersByPrice)
        {
            target = target.NextEntry!;
            if (newOrdersAtPrice.Side == Side.Buy)
            {
                addAfter = newOrdersAtPrice.Price < target.Price;
            }
            else
            {
                addAfter = newOrdersAtPrice.Price > target.Price;
            }
        }

        if (addAfter)
        {
            newOrdersAtPrice.PrevEntry = target;
            newOrdersAtPrice.NextEntry = target.NextEntry;
            target.NextEntry!.PrevEntry = newOrdersAtPrice;
            target.NextEntry = newOrdersAtPrice;
        }
        else
        {
            newOrdersAtPrice.PrevEntry = target.PrevEntry;
            newOrdersAtPrice.NextEntry = target;
            target.PrevEntry!.NextEntry = newOrdersAtPrice;
            target.PrevEntry = newOrdersAtPrice;

            if ((newOrdersAtPrice.Side == Side.Buy && newOrdersAtPrice.Price > bestOrdersByPrice.Price) ||
                (newOrdersAtPrice.Side == Side.Sell && newOrdersAtPrice.Price < bestOrdersByPrice.Price))
            {
                bestOrdersByPrice = newOrdersAtPrice;
            }
        }
    }

    private void RemoveOrdersAtPrice(Side side, long price)
    {
        ref var bestOrdersByPrice = ref (side == Side.Buy ? ref _bidsByPrice : ref _asksByPrice);
        var priceIdx = PriceToIndex(price);
        var ordersAtPrice = _priceOrdersAtPrice[priceIdx]!;

        if (ordersAtPrice.NextEntry == ordersAtPrice)
        {
            bestOrdersByPrice = null;
        }
        else
        {
            ordersAtPrice.PrevEntry!.NextEntry = ordersAtPrice.NextEntry;
            ordersAtPrice.NextEntry!.PrevEntry = ordersAtPrice.PrevEntry;

            if (ordersAtPrice == bestOrdersByPrice)
            {
                bestOrdersByPrice = ordersAtPrice.NextEntry;
            }

            ordersAtPrice.PrevEntry = null;
            ordersAtPrice.NextEntry = null;
        }

        _priceOrdersAtPrice[priceIdx] = null;
        _ordersAtPricePool.Deallocate(ordersAtPrice);
    }
}
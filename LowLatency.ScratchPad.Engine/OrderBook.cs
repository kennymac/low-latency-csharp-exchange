using LowLatency.ScratchPad.Engine.Model;

namespace LowLatency.ScratchPad.Engine;

public sealed class OrderBook
{
    public const int MaxOrders = 10_000;
    public const int MaxPriceLevels = 1_000;
    public const int MaxClients = 100;
    public const int MaxOrdersPerClient = 1_000;
    public const long MaxSupportedPrice = 1_000_000_000L;

    private const int PriceMapCapacity = 2048; // Power of 2 (mask 2047)
    private const int PriceMapMask = PriceMapCapacity - 1;

    private const int ClientOrderMapCapacity = 32768; // Power of 2 (mask 32767)
    private const int ClientOrderMapMask = ClientOrderMapCapacity - 1;

    public uint TickerId { get; }

    private readonly IClientResponseListener? _clientResponseListener;
    private readonly IMarketUpdateListener? _marketUpdateListener;

    private readonly MemPool<Order> _orderPool;
    private readonly MemPool<OrdersAtPrice> _ordersAtPricePool;

    private OrdersAtPrice? _bidsByPrice;
    private OrdersAtPrice? _asksByPrice;

    private readonly OrdersAtPrice?[] _priceOrdersAtPrice = new OrdersAtPrice?[PriceMapCapacity];
    private readonly Order?[] _clientOrderMap = new Order?[ClientOrderMapCapacity];
    private readonly ushort[] _clientOrderCounts = new ushort[MaxClients];

    private int _activeOrdersCount;
    private int _activePriceLevelsCount;

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
    }

    private static int HashClientOrder(uint clientId, ulong clientOrderId)
    {
        ulong key = ((ulong)clientId << 32) ^ clientOrderId;
        key = (key ^ (key >> 30)) * 0xbf58476d1ce4e5b9UL;
        key = (key ^ (key >> 27)) * 0x94d049bb133111ebUL;
        return (int)((key ^ (key >> 31)) & (ulong)ClientOrderMapMask);
    }

    private Order? GetClientOrder(uint clientId, ulong clientOrderId)
    {
        if (clientId >= MaxClients) return null;
        int slot = HashClientOrder(clientId, clientOrderId);
        for (int i = 0; i < ClientOrderMapCapacity; i++)
        {
            var order = _clientOrderMap[slot];
            if (order == null) return null;
            if (order.ClientId == clientId && order.ClientOrderId == clientOrderId)
            {
                return order;
            }
            slot = (slot + 1) & ClientOrderMapMask;
        }
        return null;
    }

    private void PutClientOrder(Order order)
    {
        int slot = HashClientOrder(order.ClientId, order.ClientOrderId);
        for (int i = 0; i < ClientOrderMapCapacity; i++)
        {
            var existing = _clientOrderMap[slot];
            if (existing == null || (existing.ClientId == order.ClientId && existing.ClientOrderId == order.ClientOrderId))
            {
                _clientOrderMap[slot] = order;
                return;
            }
            slot = (slot + 1) & ClientOrderMapMask;
        }
    }

    private void RemoveClientOrder(uint clientId, ulong clientOrderId)
    {
        int slot = HashClientOrder(clientId, clientOrderId);
        int targetSlot = -1;
        for (int i = 0; i < ClientOrderMapCapacity; i++)
        {
            var order = _clientOrderMap[slot];
            if (order == null) return;
            if (order.ClientId == clientId && order.ClientOrderId == clientOrderId)
            {
                targetSlot = slot;
                break;
            }
            slot = (slot + 1) & ClientOrderMapMask;
        }

        if (targetSlot < 0) return;

        _clientOrderMap[targetSlot] = null;
        int nextSlot = (targetSlot + 1) & ClientOrderMapMask;
        while (_clientOrderMap[nextSlot] != null)
        {
            var currOrder = _clientOrderMap[nextSlot]!;
            int originalSlot = HashClientOrder(currOrder.ClientId, currOrder.ClientOrderId);

            bool shouldRelocate = false;
            if (nextSlot >= targetSlot)
            {
                shouldRelocate = (originalSlot <= targetSlot || originalSlot > nextSlot);
            }
            else
            {
                shouldRelocate = (originalSlot <= targetSlot && originalSlot > nextSlot);
            }

            if (shouldRelocate)
            {
                _clientOrderMap[targetSlot] = currOrder;
                _clientOrderMap[nextSlot] = null;
                targetSlot = nextSlot;
            }
            nextSlot = (nextSlot + 1) & ClientOrderMapMask;
        }
    }

    private static int HashPrice(Side side, long price)
    {
        ulong key = ((ulong)side << 60) ^ (ulong)price;
        key = (key ^ (key >> 30)) * 0xbf58476d1ce4e5b9UL;
        return (int)((key ^ (key >> 27)) & (ulong)PriceMapMask);
    }

    private OrdersAtPrice? GetOrdersAtPrice(Side side, long price)
    {
        int slot = HashPrice(side, price);
        for (int i = 0; i < PriceMapCapacity; i++)
        {
            var entry = _priceOrdersAtPrice[slot];
            if (entry == null) return null;
            if (entry.Price == price && entry.Side == side) return entry;
            slot = (slot + 1) & PriceMapMask;
        }
        return null;
    }

    private void PutOrdersAtPrice(OrdersAtPrice entry)
    {
        int slot = HashPrice(entry.Side, entry.Price);
        for (int i = 0; i < PriceMapCapacity; i++)
        {
            var existing = _priceOrdersAtPrice[slot];
            if (existing == null || (existing.Price == entry.Price && existing.Side == entry.Side))
            {
                _priceOrdersAtPrice[slot] = entry;
                return;
            }
            slot = (slot + 1) & PriceMapMask;
        }
    }

    private void RemoveOrdersAtPriceFromMap(Side side, long price)
    {
        int slot = HashPrice(side, price);
        int targetSlot = -1;
        for (int i = 0; i < PriceMapCapacity; i++)
        {
            var entry = _priceOrdersAtPrice[slot];
            if (entry == null) return;
            if (entry.Price == price && entry.Side == side)
            {
                targetSlot = slot;
                break;
            }
            slot = (slot + 1) & PriceMapMask;
        }

        if (targetSlot < 0) return;

        _priceOrdersAtPrice[targetSlot] = null;
        int nextSlot = (targetSlot + 1) & PriceMapMask;
        while (_priceOrdersAtPrice[nextSlot] != null)
        {
            var currEntry = _priceOrdersAtPrice[nextSlot]!;
            int originalSlot = HashPrice(currEntry.Side, currEntry.Price);

            bool shouldRelocate = false;
            if (nextSlot >= targetSlot)
            {
                shouldRelocate = (originalSlot <= targetSlot || originalSlot > nextSlot);
            }
            else
            {
                shouldRelocate = (originalSlot <= targetSlot && originalSlot > nextSlot);
            }

            if (shouldRelocate)
            {
                _priceOrdersAtPrice[targetSlot] = currEntry;
                _priceOrdersAtPrice[nextSlot] = null;
                targetSlot = nextSlot;
            }
            nextSlot = (nextSlot + 1) & PriceMapMask;
        }
    }

    public void Add(
        uint clientId,
        ulong clientOrderId,
        Side side,
        long price,
        uint qty)
    {
        // 1. Price Bounds Check
        if (price <= 0 || price > MaxSupportedPrice)
        {
            _clientResponseListener?.OnClientResponse(new ClientResponse(
                Type: ClientResponseType.Rejected,
                ClientId: clientId,
                TickerId: TickerId,
                ClientOrderId: clientOrderId,
                MarketOrderId: 0,
                Side: side,
                Price: price,
                ExecQty: 0,
                LeavesQty: 0));
            return;
        }

        // 2. Client ID Bounds Check
        if (clientId >= MaxClients)
        {
            _clientResponseListener?.OnClientResponse(new ClientResponse(
                Type: ClientResponseType.Rejected,
                ClientId: clientId,
                TickerId: TickerId,
                ClientOrderId: clientOrderId,
                MarketOrderId: 0,
                Side: side,
                Price: price,
                ExecQty: 0,
                LeavesQty: 0));
            return;
        }

        // 3. Per-Client Order Quota Check
        if (_clientOrderCounts[clientId] >= MaxOrdersPerClient)
        {
            _clientResponseListener?.OnClientResponse(new ClientResponse(
                Type: ClientResponseType.Rejected,
                ClientId: clientId,
                TickerId: TickerId,
                ClientOrderId: clientOrderId,
                MarketOrderId: 0,
                Side: side,
                Price: price,
                ExecQty: 0,
                LeavesQty: 0));
            return;
        }

        // 4. Max Active Orders Limit Check
        if (_activeOrdersCount >= MaxOrders)
        {
            _clientResponseListener?.OnClientResponse(new ClientResponse(
                Type: ClientResponseType.Rejected,
                ClientId: clientId,
                TickerId: TickerId,
                ClientOrderId: clientOrderId,
                MarketOrderId: 0,
                Side: side,
                Price: price,
                ExecQty: 0,
                LeavesQty: 0));
            return;
        }

        // 5. Max Active Price Levels Check
        var existingOrdersAtPrice = GetOrdersAtPrice(side, price);
        if (existingOrdersAtPrice == null && _activePriceLevelsCount >= MaxPriceLevels)
        {
            _clientResponseListener?.OnClientResponse(new ClientResponse(
                Type: ClientResponseType.Rejected,
                ClientId: clientId,
                TickerId: TickerId,
                ClientOrderId: clientOrderId,
                MarketOrderId: 0,
                Side: side,
                Price: price,
                ExecQty: 0,
                LeavesQty: 0));
            return;
        }

        var marketOrderId = _nextMarketOrderId++;

        _clientResponseListener?.OnClientResponse(new ClientResponse(
            Type: ClientResponseType.Accepted,
            ClientId: clientId,
            TickerId: TickerId,
            ClientOrderId: clientOrderId,
            MarketOrderId: marketOrderId,
            Side: side,
            Price: price,
            ExecQty: 0,
            LeavesQty: qty));

        var leavesQty = CheckForMatch(
            clientId: clientId,
            clientOrderId: clientOrderId,
            side: side,
            price: price,
            qty: qty,
            marketOrderId: marketOrderId);

        if (leavesQty > 0)
        {
            var priority = GetNextPriority(side, price);

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
                Type: MarketUpdateType.Add,
                MarketOrderId: marketOrderId,
                TickerId: TickerId,
                Side: side,
                Price: price,
                Qty: leavesQty,
                Priority: priority));
        }
    }

    public void Cancel(
        uint clientId,
        ulong clientOrderId)
    {
        var order = GetClientOrder(clientId: clientId, clientOrderId: clientOrderId);

        if (order == null || order.ClientId != clientId || order.ClientOrderId != clientOrderId)
        {
            _clientResponseListener?.OnClientResponse(new ClientResponse(
                Type: ClientResponseType.CancelRejected,
                ClientId: clientId,
                TickerId: TickerId,
                ClientOrderId: clientOrderId,
                MarketOrderId: 0,
                Side: Side.Invalid,
                Price: 0,
                ExecQty: 0,
                LeavesQty: 0));
            return;
        }

        _clientResponseListener?.OnClientResponse(new ClientResponse(
            Type: ClientResponseType.Canceled,
            ClientId: clientId,
            TickerId: TickerId,
            ClientOrderId: clientOrderId,
            MarketOrderId: order.MarketOrderId,
            Side: order.Side,
            Price: order.Price,
            ExecQty: 0,
            LeavesQty: order.Qty));

        _marketUpdateListener?.OnMarketUpdate(new MarketUpdate(
            Type: MarketUpdateType.Cancel,
            MarketOrderId: order.MarketOrderId,
            TickerId: TickerId,
            Side: order.Side,
            Price: order.Price,
            Qty: 0,
            Priority: order.Priority));

        RemoveOrder(order);
    }

    public Order? GetOrder(
        uint clientId,
        ulong clientOrderId)
    {
        return GetClientOrder(clientId: clientId, clientOrderId: clientOrderId);
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
            Type: ClientResponseType.Filled,
            ClientId: takerClientId,
            TickerId: TickerId,
            ClientOrderId: takerClientOrderId,
            MarketOrderId: takerMarketOrderId,
            Side: takerSide,
            Price: makerOrder.Price,
            ExecQty: fillQty,
            LeavesQty: leavesQty));

        // Response to Maker
        _clientResponseListener?.OnClientResponse(new ClientResponse(
            Type: ClientResponseType.Filled,
            ClientId: makerOrder.ClientId,
            TickerId: TickerId,
            ClientOrderId: makerOrder.ClientOrderId,
            MarketOrderId: makerOrder.MarketOrderId,
            Side: makerOrder.Side,
            Price: makerOrder.Price,
            ExecQty: fillQty,
            LeavesQty: makerOrder.Qty));

        // Trade Market Update
        _marketUpdateListener?.OnMarketUpdate(new MarketUpdate(
            Type: MarketUpdateType.Trade,
            MarketOrderId: 0,
            TickerId: TickerId,
            Side: takerSide,
            Price: makerOrder.Price,
            Qty: fillQty,
            Priority: 0));

        if (makerOrder.Qty == 0)
        {
            _marketUpdateListener?.OnMarketUpdate(new MarketUpdate(
                Type: MarketUpdateType.Cancel,
                MarketOrderId: makerOrder.MarketOrderId,
                TickerId: TickerId,
                Side: makerOrder.Side,
                Price: makerOrder.Price,
                Qty: makerOrderQty,
                Priority: 0));

            RemoveOrder(makerOrder);
        }
        else
        {
            _marketUpdateListener?.OnMarketUpdate(new MarketUpdate(
                Type: MarketUpdateType.Modify,
                MarketOrderId: makerOrder.MarketOrderId,
                TickerId: TickerId,
                Side: makerOrder.Side,
                Price: makerOrder.Price,
                Qty: makerOrder.Qty,
                Priority: makerOrder.Priority));
        }
    }

    private ulong GetNextPriority(Side side, long price)
    {
        var ordersAtPrice = GetOrdersAtPrice(side, price);
        if (ordersAtPrice != null && ordersAtPrice.FirstOrder != null)
        {
            return ordersAtPrice.FirstOrder.PrevOrder!.Priority + 1;
        }
        return 1;
    }

    private void AddOrder(Order order)
    {
        var ordersAtPrice = GetOrdersAtPrice(order.Side, order.Price);

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

            _activePriceLevelsCount++;
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

        _activeOrdersCount++;
        _clientOrderCounts[order.ClientId]++;
        PutClientOrder(order);
    }

    private void RemoveOrder(Order order)
    {
        var ordersAtPrice = GetOrdersAtPrice(order.Side, order.Price)!;

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

        RemoveClientOrder(order.ClientId, order.ClientOrderId);
        _clientOrderCounts[order.ClientId]--;
        _activeOrdersCount--;
        _orderPool.Deallocate(order);
    }

    private void AddOrdersAtPrice(OrdersAtPrice newOrdersAtPrice)
    {
        PutOrdersAtPrice(newOrdersAtPrice);

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
        var ordersAtPrice = GetOrdersAtPrice(side, price)!;

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

        RemoveOrdersAtPriceFromMap(side, price);
        _activePriceLevelsCount--;
        _ordersAtPricePool.Deallocate(ordersAtPrice);
    }
}
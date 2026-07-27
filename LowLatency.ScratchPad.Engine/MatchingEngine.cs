using LowLatency.ScratchPad.Engine.Model;

namespace LowLatency.ScratchPad.Engine;

public sealed class MatchingEngine
{
    public const int MaxTickers = 100;
    private readonly OrderBook?[] _orderBooks = new OrderBook?[MaxTickers];
    private readonly IClientResponseListener? _clientResponseListener;
    private readonly IMarketUpdateListener? _marketUpdateListener;

    public MatchingEngine(
        IClientResponseListener? clientResponseListener = null,
        IMarketUpdateListener? marketUpdateListener = null)
    {
        _clientResponseListener = clientResponseListener;
        _marketUpdateListener = marketUpdateListener;

        _orderBooks[1] = new OrderBook(
            tickerId: 1,
            clientResponseListener: _clientResponseListener,
            marketUpdateListener: _marketUpdateListener);
    }

    public OrderBook GetOrCreateOrderBook(uint tickerId)
    {
        var idx = (int)(tickerId % MaxTickers);
        var orderBook = _orderBooks[idx];
        if (orderBook == null)
        {
            orderBook = new OrderBook(
                tickerId: tickerId,
                clientResponseListener: _clientResponseListener,
                marketUpdateListener: _marketUpdateListener);
            _orderBooks[idx] = orderBook;
        }

        return orderBook;
    }

    public void ProcessOrder(
        uint clientId,
        ulong clientOrderId,
        uint tickerId,
        Side side,
        long price,
        uint qty)
    {
        var orderBook = GetOrCreateOrderBook(tickerId);
        orderBook.Add(
            clientId: clientId,
            clientOrderId: clientOrderId,
            side: side,
            price: price,
            qty: qty);
    }

    public void CancelOrder(
        uint clientId,
        ulong clientOrderId,
        uint tickerId)
    {
        var idx = (int)(tickerId % MaxTickers);
        var orderBook = _orderBooks[idx];
        if (orderBook != null)
        {
            orderBook.Cancel(
                clientId: clientId,
                clientOrderId: clientOrderId);
        }
        else
        {
            _clientResponseListener?.OnClientResponse(new ClientResponse(
                type: ClientResponseType.CancelRejected,
                clientId: clientId,
                tickerId: tickerId,
                clientOrderId: clientOrderId,
                marketOrderId: 0,
                side: Side.Invalid,
                price: 0,
                execQty: 0,
                leavesQty: 0));
        }
    }
}
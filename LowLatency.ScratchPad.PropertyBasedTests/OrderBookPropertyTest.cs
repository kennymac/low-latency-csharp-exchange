using FsCheck.Xunit;
using LowLatency.ScratchPad.Engine;
using LowLatency.ScratchPad.Engine.Model;

namespace LowLatency.ScratchPad.PropertyBasedTests;

public class OrderBookPropertyTest : IClientResponseListener
{
    private ClientResponse? _lastResponse;

    void IClientResponseListener.OnClientResponse(in ClientResponse response)
    {
        _lastResponse = response;
    }

    [Property(MaxTest = 200)]
    public bool Add_GivenArbitraryPrice_EnforcesValidationOrRestingInvariance(long price)
    {
        _lastResponse = null;
        var book = new OrderBook(tickerId: 1, clientResponseListener: this, marketUpdateListener: null);

        // Act
        book.Add(clientId: 1, clientOrderId: 100, side: Side.Buy, price: price, qty: 10);

        if (price <= 0 || price > OrderBook.MaxSupportedPrice)
        {
            // Invariant 1: Invalid price must be rejected gracefully without state corruption or throwing exceptions
            var isRejected = _lastResponse.HasValue && _lastResponse.Value.Type == ClientResponseType.Rejected;
            var isNullInBook = book.GetOrder(clientId: 1, clientOrderId: 100) == null;
            var isBidsNull = book.BidsByPrice == null;
            return isRejected && isNullInBook && isBidsNull;
        }
        else
        {
            // Invariant 2: Valid price must be accepted and retrievable at exact price level
            var isAccepted = _lastResponse.HasValue && _lastResponse.Value.Type == ClientResponseType.Accepted;
            var order = book.GetOrder(clientId: 1, clientOrderId: 100);
            var isOrderFound = order != null && order.Price == price;
            return isAccepted && isOrderFound;
        }
    }

    [Property(MaxTest = 200)]
    public bool Cancel_GivenArbitraryClientOrderIds_BothOrdersCanBeCancelled(ulong clientOrderId1, ulong clientOrderId2)
    {
        // Avoid duplicate ID case in property generator
        if (clientOrderId1 == clientOrderId2)
        {
            return true;
        }

        var responses = new List<ClientResponse>();
        var listener = new ClientResponseCollector(responses);
        var book = new OrderBook(tickerId: 1, clientResponseListener: listener, marketUpdateListener: null);

        // Act: Add two distinct orders for Client 1 with arbitrary 64-bit Order IDs
        book.Add(clientId: 1, clientOrderId: clientOrderId1, side: Side.Buy, price: 100, qty: 10);
        book.Add(clientId: 1, clientOrderId: clientOrderId2, side: Side.Buy, price: 102, qty: 10);

        responses.Clear();

        // Cancel the first order
        book.Cancel(clientId: 1, clientOrderId: clientOrderId1);

        // Invariant: First order must cancel successfully, and second order must still be retrievable
        var canceledSuccess = responses.Count == 1 && responses[0].Type == ClientResponseType.Canceled && responses[0].ClientOrderId == clientOrderId1;
        var order2StillResting = book.GetOrder(clientId: 1, clientOrderId: clientOrderId2) != null;

        return canceledSuccess && order2StillResting;
    }

    private sealed class ClientResponseCollector : IClientResponseListener
    {
        private readonly List<ClientResponse> _responses;

        public ClientResponseCollector(List<ClientResponse> responses)
        {
            _responses = responses;
        }

        public void OnClientResponse(in ClientResponse response)
        {
            _responses.Add(response);
        }
    }
}

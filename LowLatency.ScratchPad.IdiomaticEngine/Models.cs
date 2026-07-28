namespace LowLatency.ScratchPad.IdiomaticEngine.Model;

public enum Side : byte
{
    Invalid = 0,
    Buy = 1,
    Sell = 2
}

public enum ClientResponseType : byte
{
    Accepted = 1,
    Filled = 2,
    Canceled = 3,
    CancelRejected = 4
}

public enum MarketUpdateType : byte
{
    Add = 1,
    Modify = 2,
    Cancel = 3,
    Trade = 4
}

public class Order
{
    public uint ClientId { get; set; }
    public ulong ClientOrderId { get; set; }
    public ulong MarketOrderId { get; set; }
    public uint TickerId { get; set; }
    public Side Side { get; set; }
    public long Price { get; set; }
    public uint Qty { get; set; }
    public ulong Priority { get; set; }
}

public class ClientResponse
{
    public ClientResponseType Type { get; set; }
    public uint ClientId { get; set; }
    public uint TickerId { get; set; }
    public ulong ClientOrderId { get; set; }
    public ulong MarketOrderId { get; set; }
    public Side Side { get; set; }
    public long Price { get; set; }
    public uint ExecQty { get; set; }
    public uint LeavesQty { get; set; }

    public ClientResponse(
        ClientResponseType type,
        uint clientId,
        uint tickerId,
        ulong clientOrderId,
        ulong marketOrderId,
        Side side,
        long price,
        uint execQty,
        uint leavesQty)
    {
        Type = type;
        ClientId = clientId;
        TickerId = tickerId;
        ClientOrderId = clientOrderId;
        MarketOrderId = marketOrderId;
        Side = side;
        Price = price;
        ExecQty = execQty;
        LeavesQty = leavesQty;
    }
}

public class MarketUpdate
{
    public MarketUpdateType Type { get; set; }
    public ulong MarketOrderId { get; set; }
    public uint TickerId { get; set; }
    public Side Side { get; set; }
    public long Price { get; set; }
    public uint Qty { get; set; }
    public ulong Priority { get; set; }

    public MarketUpdate(
        MarketUpdateType type,
        ulong marketOrderId,
        uint tickerId,
        Side side,
        long price,
        uint qty,
        ulong priority)
    {
        Type = type;
        MarketOrderId = marketOrderId;
        TickerId = tickerId;
        Side = side;
        Price = price;
        Qty = qty;
        Priority = priority;
    }
}

public interface IClientResponseListener
{
    void OnClientResponse(ClientResponse response);
}

public interface IMarketUpdateListener
{
    void OnMarketUpdate(MarketUpdate update);
}

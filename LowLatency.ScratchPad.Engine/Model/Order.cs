namespace LowLatency.ScratchPad.Engine.Model;

public sealed class Order
{
    public uint TickerId { get; set; }
    public uint ClientId { get; set; }
    public ulong ClientOrderId { get; set; }
    public ulong MarketOrderId { get; set; }
    public Side Side { get; set; }
    public long Price { get; set; }
    public uint Qty { get; set; }
    public ulong Priority { get; set; }

    public Order? PrevOrder { get; set; }
    public Order? NextOrder { get; set; }
}

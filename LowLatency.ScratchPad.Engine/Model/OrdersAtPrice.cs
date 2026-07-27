namespace LowLatency.ScratchPad.Engine.Model;

public sealed class OrdersAtPrice
{
    public Side Side { get; set; }
    public long Price { get; set; }
    public Order? FirstOrder { get; set; }
    public OrdersAtPrice? PrevEntry { get; set; }
    public OrdersAtPrice? NextEntry { get; set; }
}

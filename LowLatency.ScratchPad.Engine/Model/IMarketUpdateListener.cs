namespace LowLatency.ScratchPad.Engine.Model;

public interface IMarketUpdateListener
{
    void OnMarketUpdate(in MarketUpdate update);
}

using LowLatency.ScratchPad.Engine;
using LowLatency.ScratchPad.Engine.Model;

var engine = new MatchingEngine();
engine.ProcessOrder(
    clientId: 1,
    clientOrderId: 100,
    tickerId: 1,
    side: Side.Buy,
    price: 100,
    qty: 10);

Console.WriteLine("LowLatency.ScratchPad.Engine initialized.");

namespace LowLatency.ScratchPad.Engine.Model;

public interface IClientResponseListener
{
    void OnClientResponse(in ClientResponse response);
}

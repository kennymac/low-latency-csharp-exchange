namespace LowLatency.ScratchPad.Engine.Model;

public enum ClientResponseType : byte
{
    Accepted = 1,
    Canceled = 2,
    Filled = 3,
    CancelRejected = 4,
    Rejected = 5
}


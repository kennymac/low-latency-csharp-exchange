using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace LowLatency.Profiler;

public class ProcessProfiler
{
    public long MeasureAllocationsInChildProcess(string exePath)
    {
        var psi = new ProcessStartInfo(exePath) { RedirectStandardOutput = true };
        var process = Process.Start(psi) ?? throw new Exception("Failed to start process");

        // Create an EventPipe session targeting the spawned process PID
        var providers = new[]
        {
            new EventPipeProvider("Microsoft-Windows-DotNETRuntime", 
                EventLevel.Verbose, 
                (long)ClrTraceEventParser.Keywords.GC) // Capture GC Allocation ticks
        };

        var client = new DiagnosticsClient(process.Id);
        using var session = client.StartEventPipeSession(providers, false);
        using var source = new EventPipeEventSource(session.EventStream);

        long totalBytesAllocated = 0;

        // Parse runtime allocation events in real-time
        source.Clr.GCAllocationTick += data =>
        {
            totalBytesAllocated += data.AllocationAmount;
        };

        // Run tracing in background while child process completes
        Task.Run(() => source.Process());
        process.WaitForExit();

        return totalBytesAllocated;
    }
}
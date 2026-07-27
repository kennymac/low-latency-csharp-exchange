#pragma warning disable CS8602
#pragma warning disable xUnit1051

using System;
using System.IO;
using LowLatency.ScratchPad.Engine;
using LowLatency.ScratchPad.Engine.Model;
using Xunit;
using AwesomeAssertions;

namespace LowLatency.ScratchPad.MatchingEngine.UnitTests;

public class LowLatencyLoggerTest
{
    [Fact]
    public void Log_GivenLogEntry_ThenEnqueuesSuccessfullyWithoutBlocking()
    {
        // Arrange
        using var writer = new StringWriter();
        using var logger = new LowLatencyLogger(writer: writer, ringCapacity: 16, startBackgroundFlusher: false);

        // Act
        var result = logger.Log(
            level: LogLevel.Info,
            tickerId: 1,
            clientId: 10,
            clientOrderId: 1_001,
            price: 150,
            qty: 100);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Flush_GivenPendingLogEntries_ThenFlushesAllEntriesToTextWriter()
    {
        // Arrange
        using var writer = new StringWriter();
        using var logger = new LowLatencyLogger(writer: writer, ringCapacity: 16, startBackgroundFlusher: false);

        logger.Log(
            level: LogLevel.Info,
            tickerId: 1,
            clientId: 10,
            clientOrderId: 1_001,
            price: 150,
            qty: 100);

        logger.Log(
            level: LogLevel.Warning,
            tickerId: 1,
            clientId: 20,
            clientOrderId: 1_002,
            price: 155,
            qty: 50);

        // Act
        var flushedCount = logger.Flush();
        var output = writer.ToString();

        // Assert
        flushedCount.Should().Be(2);
        output.Should().Contain("[Info] Ticker:1 Client:10 OID:1001 Price:150 Qty:100");
        output.Should().Contain("[Warning] Ticker:1 Client:20 OID:1002 Price:155 Qty:50");
    }

    [Fact]
    public void Log_GivenBulkLoggingFlow_ThenZeroGcAllocations()
    {
        // Arrange
        using var writer = new StringWriter();
        using var logger = new LowLatencyLogger(writer: writer, ringCapacity: 16_384, startBackgroundFlusher: false);

        // Warm-up phase: JIT compile Log method paths
        for (var i = 0uL; i < 50uL; i++)
        {
            logger.Log(
                level: LogLevel.Info,
                tickerId: 1,
                clientId: 1,
                clientOrderId: i,
                price: 100,
                qty: 10);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();

        // Act: Post 10,000 log entries on hot path (pure lock-free enqueue)
        for (var i = 100uL; i < 10_100uL; i++)
        {
            logger.Log(
                level: LogLevel.Info,
                tickerId: 1,
                clientId: (uint)(i % 50) + 1,
                clientOrderId: i,
                price: 150 + (long)(i % 10),
                qty: 5);
        }

        var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        var totalAllocatedBytes = bytesAfter - bytesBefore;

        // Assert zero bytes allocated on the managed heap
        totalAllocatedBytes.Should().Be(0, "no bytes should be allocated to the managed heap");
    }
}

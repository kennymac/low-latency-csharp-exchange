```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD


```
| Method                            | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------------------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| SpscRingBuffer_PushAndPop         |  1.279 ns | 0.0153 ns | 0.0143 ns |  1.00 |    0.02 |         - |          NA |
| ConcurrentQueue_PushAndPop        |  2.974 ns | 0.0299 ns | 0.0279 ns |  2.33 |    0.03 |         - |          NA |
| SystemThreadingChannel_PushAndPop | 15.614 ns | 0.1460 ns | 0.1294 ns | 12.21 |    0.16 |         - |          NA |

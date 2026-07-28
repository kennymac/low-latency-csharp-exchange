```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M1, 1 CPU, 8 logical and 8 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.1026.32716), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.10 (10.0.1026.32716), Arm64 RyuJIT AdvSIMD


```
| Method                            | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------------------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| SpscRingBuffer_PushAndPop         |  9.582 ns | 0.0412 ns | 0.0322 ns |  1.00 |    0.00 |         - |          NA |
| ConcurrentQueue_PushAndPop        | 11.087 ns | 0.1049 ns | 0.0930 ns |  1.16 |    0.01 |         - |          NA |
| SystemThreadingChannel_PushAndPop | 26.130 ns | 0.3605 ns | 0.3372 ns |  2.73 |    0.04 |         - |          NA |

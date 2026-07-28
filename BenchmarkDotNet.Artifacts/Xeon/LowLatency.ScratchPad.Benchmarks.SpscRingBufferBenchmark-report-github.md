```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Intel Xeon W-3245 CPU 3.20GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2


```
| Method                            | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------------------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| SpscRingBuffer_PushAndPop         |  2.055 ns | 0.0127 ns | 0.0112 ns |  1.00 |    0.01 |         - |          NA |
| ConcurrentQueue_PushAndPop        | 12.251 ns | 0.0420 ns | 0.0393 ns |  5.96 |    0.04 |         - |          NA |
| SystemThreadingChannel_PushAndPop | 45.309 ns | 0.1251 ns | 0.1171 ns | 22.05 |    0.13 |         - |          NA |

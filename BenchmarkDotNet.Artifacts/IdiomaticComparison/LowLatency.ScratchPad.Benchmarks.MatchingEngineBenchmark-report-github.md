```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD


```
| Method                         | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| MatchOrderPair_ZeroAllocation  | 25.16 ns | 1.852 ns | 5.460 ns | 23.44 ns |  1.04 |    0.30 |      - |         - |          NA |
| MatchOrderPair_IdiomaticCSharp | 53.51 ns | 0.689 ns | 0.575 ns | 53.42 ns |  2.22 |    0.42 | 0.0392 |     328 B |          NA |

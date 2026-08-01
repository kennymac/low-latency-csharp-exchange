```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD


```
| Method                         | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| MatchOrderPair_ZeroAllocation  | 35.08 ns | 0.720 ns | 1.669 ns |  1.00 |    0.07 |      - |         - |          NA |
| MatchOrderPair_IdiomaticCSharp | 55.50 ns | 0.281 ns | 0.235 ns |  1.59 |    0.08 | 0.0392 |     328 B |          NA |

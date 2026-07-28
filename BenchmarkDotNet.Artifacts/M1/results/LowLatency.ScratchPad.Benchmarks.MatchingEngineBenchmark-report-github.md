```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M1, 1 CPU, 8 logical and 8 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.1026.32716), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.10 (10.0.1026.32716), Arm64 RyuJIT AdvSIMD


```
| Method         | Mean     | Error    | StdDev   | Allocated |
|--------------- |---------:|---------:|---------:|----------:|
| MatchOrderPair | 35.10 ns | 0.609 ns | 0.652 ns |         - |

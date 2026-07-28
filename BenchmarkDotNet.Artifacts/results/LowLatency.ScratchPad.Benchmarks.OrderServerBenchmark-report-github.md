```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M3 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD


```
| Method                      | Mean     | Error     | StdDev    | Allocated |
|---------------------------- |---------:|----------:|----------:|----------:|
| DecodeAndEnqueueBinaryFrame | 2.788 ns | 0.0287 ns | 0.0268 ns |         - |

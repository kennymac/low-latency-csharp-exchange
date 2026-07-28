```

BenchmarkDotNet v0.14.0, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Intel Xeon W-3245 CPU 3.20GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2


```
| Method                      | Mean     | Error     | StdDev    | Allocated |
|---------------------------- |---------:|----------:|----------:|----------:|
| DecodeAndEnqueueBinaryFrame | 4.499 ns | 0.0178 ns | 0.0166 ns |         - |

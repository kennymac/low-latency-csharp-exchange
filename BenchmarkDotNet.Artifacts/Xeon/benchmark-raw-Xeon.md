# Raw Xeon BenchmarkDotNet Results

**Hardware:** Intel Xeon W-3245 CPU @ 3.20GHz (16 physical cores, 32 logical cores)  
**OS:** macOS 15.5 (Darwin 25.5.0)  
**Runtime:** .NET 10.0.10 (X64 RyuJIT AVX2)

---

## 1. Lock-Free SPSC Ring Buffer Benchmark

```text
| Method                            | Mean      | Error     | StdDev    | Ratio | Allocated |
|---------------------------------- |----------:|----------:|----------:|------:|----------:|
| SpscRingBuffer_PushAndPop         |  2.055 ns | 0.0127 ns | 0.0112 ns |  1.00 |       0 B |
| ConcurrentQueue_PushAndPop        | 12.251 ns | 0.0420 ns | 0.0393 ns |  5.96 |       0 B |
| SystemThreadingChannel_PushAndPop | 45.309 ns | 0.1251 ns | 0.1171 ns | 22.05 |       0 B |
```

---

## 2. Order Gateway Wire Decoding Benchmark (`OrderServer`)

```text
| Method                      | Mean     | Error     | StdDev    | Allocated |
|---------------------------- |---------:|----------:|----------:|----------:|
| DecodeAndEnqueueBinaryFrame | 4.361 ns | 0.0124 ns | 0.0103 ns |       0 B |
```

---

## 3. Matching Engine Order Pair Match Benchmark (`OrderBook`)

```text
| Method         | Mean     | Error    | StdDev   | Allocated |
|--------------- |---------:|---------:|---------:|----------:|
| MatchOrderPair | 45.31 ns | 0.125 ns | 0.117 ns |       0 B |
```

```

BenchmarkDotNet v0.13.12, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon Processor 2.30GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2


```
| Method            | NumberOfAgents | Mean         | Error       | StdDev      | Ratio | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------ |--------------- |-------------:|------------:|------------:|------:|-------:|-------:|----------:|------------:|
| **Baseline_List**     | **10**             |     **514.5 ns** |     **1.52 ns** |     **1.42 ns** |  **1.00** | **0.0134** |      **-** |     **320 B** |        **1.00** |
| Optimized_HashSet | 10             |     896.6 ns |     2.52 ns |     2.36 ns |  1.74 | 0.0372 |      - |     888 B |        2.77 |
|                   |                |              |             |             |       |        |        |           |             |
| **Baseline_List**     | **50**             |   **7,007.9 ns** |    **10.93 ns** |     **9.69 ns** |  **1.00** | **0.0534** |      **-** |    **1280 B** |        **1.00** |
| Optimized_HashSet | 50             |   4,186.6 ns |    15.28 ns |    14.30 ns |  0.60 | 0.1678 |      - |    4008 B |        3.13 |
|                   |                |              |             |             |       |        |        |           |             |
| **Baseline_List**     | **100**            |  **21,014.3 ns** |    **72.96 ns** |    **64.68 ns** |  **1.00** | **0.0916** |      **-** |    **2480 B** |        **1.00** |
| Optimized_HashSet | 100            |   8,405.5 ns |    48.12 ns |    45.01 ns |  0.40 | 0.2899 |      - |    7128 B |        2.87 |
|                   |                |              |             |             |       |        |        |           |             |
| **Baseline_List**     | **500**            | **529,251.3 ns** | **1,208.85 ns** | **1,071.61 ns** |  **1.00** |      **-** |      **-** |   **12081 B** |        **1.00** |
| Optimized_HashSet | 500            |  45,354.7 ns |   117.90 ns |   104.52 ns |  0.09 | 1.3428 | 0.1221 |   32688 B |        2.71 |

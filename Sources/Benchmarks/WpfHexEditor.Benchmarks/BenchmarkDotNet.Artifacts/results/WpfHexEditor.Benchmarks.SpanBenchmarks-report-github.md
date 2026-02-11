```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7628)
12th Gen Intel Core i9-12900H, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.100-rc.2.25502.107
  [Host]     : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX2
  Job-QLFFIZ : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX2
  Dry        : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX2


```
| Method            | Job        | IterationCount | LaunchCount | RunStrategy | UnrollFactor | WarmupCount | ChunkSize | Mean      | Error    | StdDev   | Gen0      | Completed Work Items | Lock Contentions | Allocated |
|------------------ |----------- |--------------- |------------ |------------ |------------- |------------ |---------- |----------:|---------:|---------:|----------:|---------------------:|-----------------:|----------:|
| **SpanWithArrayPool** | **Job-QLFFIZ** | **10**             | **Default**     | **Default**     | **16**           | **3**           | **1024**      |  **31.65 ms** | **1.362 ms** | **0.901 ms** | **8000.0000** |                    **-** |                **-** |     **96 MB** |
| SpanWithArrayPool | Dry        | 1              | 1           | ColdStart   | 1            | 1           | 1024      | 121.17 ms |       NA | 0.000 ms | 8000.0000 |                    - |                - |     96 MB |
| **SpanWithArrayPool** | **Job-QLFFIZ** | **10**             | **Default**     | **Default**     | **16**           | **3**           | **8192**      |  **30.06 ms** | **0.545 ms** | **0.285 ms** | **8000.0000** |                    **-** |                **-** |     **96 MB** |
| SpanWithArrayPool | Dry        | 1              | 1           | ColdStart   | 1            | 1           | 8192      | 106.51 ms |       NA | 0.000 ms | 8000.0000 |                    - |                - |  96.01 MB |
| **SpanWithArrayPool** | **Job-QLFFIZ** | **10**             | **Default**     | **Default**     | **16**           | **3**           | **65536**     |  **31.01 ms** | **2.227 ms** | **1.473 ms** | **8000.0000** |                    **-** |                **-** |     **96 MB** |
| SpanWithArrayPool | Dry        | 1              | 1           | ColdStart   | 1            | 1           | 65536     | 106.24 ms |       NA | 0.000 ms | 8000.0000 |                    - |                - |  96.06 MB |

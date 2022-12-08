# CoreCLRUnityBenchmark

## ParallelPI

Inspired by [.NET Core parallel computation of PI example by Microsoft](https://learn.microsoft.com/en-us/samples/dotnet/samples/parallel-programming-compute-pi-cs/)

Calculating integral of 4/(1 + x^2) over 0 to 1, which should return PI. Integral is approximated by weighted sum of sample points i / N, i = 0 .. N - 1.


to use dotnet NativeAOT, run `dotnet publish -c Release` in `ParallelPI\CoreCLR\ParallelPI`

CoreCLR的 `ParallelAutoChunk` 和 `ParallelInline` 不是benchmark的目的，其算法和其他被比较任务不一致，仅用于测试正确性和纯算力比较。

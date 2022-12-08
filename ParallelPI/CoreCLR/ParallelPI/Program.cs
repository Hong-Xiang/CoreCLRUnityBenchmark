using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive;
using System.Reactive.Concurrency;


struct PiCalcuateChunk
{
    public required long Samples { get; init; }
    public required double RelativeOffset { get; init; }
    public double Calculate()
    {
        double result = 0.0;
        double step = 1.0 / Samples;
        for (var i = 0; i < Samples; i++)
        {
            var x = (i + RelativeOffset) * step;
            var f = 4.0 / (1.0 + x * x);
            result += f * step;
        }
        return result;
    }
}

interface PiScheduler
{
    void Run(int chunks, Action<int> job);
}

class ParallelScheduler : PiScheduler
{
    public void Run(int chunks, Action<int> job)
    {
        Parallel.For(0, chunks, (i, state) => job(i));
    }
}

class ThreadTaskScheduler : PiScheduler
{
    public void Run(int chunks, Action<int> job)
    {
        var tasks = Enumerable.Range(0, chunks).Select(i =>
                    {
                        return Task.Factory.StartNew(() =>
                                        job(i));
                    }).ToArray();
        Task.WaitAll(tasks);
    }
}

class AsyncTaskScheduler : PiScheduler
{
    public void Run(int chunks, Action<int> job)
    {
        var groups = 16;
        var groupSize = (int)Math.Ceiling((float)chunks / groups);

        var tasks = Enumerable.Range(0, groupSize).Select(async (ig) =>
                    {
                        var chunkStart = Math.Min(ig * groupSize, chunks - 1);
                        var chunkEnd = Math.Min((ig + 1) * groupSize, chunks);
                        foreach (var ic in Enumerable.Range(chunkStart, groupSize).Where(ic => ic < chunks))
                        {
                            await Task.Factory.StartNew(() => job(ic));
                        }
                    }).ToArray();
        Task.WaitAll(tasks);
    }
}

class ProfileReport
{
    public Stopwatch Stopwatch;
    public double Result;
}


namespace ParallelPI
{
    internal class Program
    {
        const int NumberOfSteps = 1_000_000_000;
        const int Chunks = 65536;
        const long ChunkSize = 128;
        //const int Chunks = 30000;

        /// <summary>Main method to time various implementations of computing PI.</summary>
        static void Main()
        {

            Console.WriteLine($"chunks {Chunks}, chunksize {ChunkSize}, totalSamples {Chunks * ChunkSize}");
            Console.WriteLine("Function                              | Elapsed Time     | Estimated Pi         | est per chunk (mu s) | Chunks / second");
            Console.WriteLine("-----------------------------------------------------------------");

            Time(new ParallelScheduler(), 8);
            Time(new ThreadTaskScheduler(), 4);
            Time(new AsyncTaskScheduler(), 8);
            Time((repreat) =>
            {
                return Enumerable.Range(0, repreat).Select((_) =>
                {
                    return ParallelAutoChunkThreadLocal(Chunks, ChunkSize);
                });
            }, nameof(ParallelAutoChunkThreadLocal), 8);
            Time((repreat) =>
            {
                return Enumerable.Range(0, repreat).Select((_) =>
                {
                    return ParallelNoChunkInline(Chunks, ChunkSize);
                });
            }, nameof(ParallelNoChunkInline), 8);


        }
        static void Time(Func<int, IEnumerable<ProfileReport>> f, string name, int repeat)
        {
            var pr = f(repeat).ToArray();
            var averageTime = pr.Select(x => x.Stopwatch.ElapsedMilliseconds).Average();
            var sw = pr.Last().Stopwatch;
            Console.WriteLine($"{name,37} | {averageTime,16} | {pr.Last().Result,20} | {averageTime * 1000 / Chunks,20} | {Chunks / sw.Elapsed.TotalSeconds}");
        }
        /// <summary>Times the execution of a function and outputs both the elapsed time and the function's result.</summary>
        static void Time(PiScheduler scheduler, int repeat)
        {
            Time((repeat) =>
            {
                return Enumerable.Range(0, repeat).Select((_) =>
                {
                    var results = new double[Chunks];
                    var sw = new Stopwatch();
                    sw.Start();
                    scheduler.Run(Chunks, (chunkIndex) =>
                                   {
                                       results[chunkIndex] = new PiCalcuateChunk { RelativeOffset = (double)chunkIndex / Chunks, Samples = ChunkSize }.Calculate();
                                   });
                    sw.Stop();
                    return new ProfileReport { Stopwatch = sw, Result = results.Average() };
                });
            }, scheduler.GetType().Name, repeat);
            var sw = Stopwatch.StartNew();
        }



        static async Task<double> SumOfPiSamples(int index, int chunkSize, double step)
        {
            return await Task.Factory.StartNew(() =>
            {
                var start = Math.Min(index * chunkSize, NumberOfSteps - 1);
                var end = Math.Min((index + 1) * chunkSize, NumberOfSteps);
                double local = 0.0;
                for (int i = start; i < end; i++)
                {
                    double x = (i + 0.5) * step;
                    local += 4.0 / (1.0 + x * x) * step;
                }
                return local;
            });
        }


        static ProfileReport ParallelAutoChunkThreadLocal(int chunks, long chunkSize)
        {
            object monitor = new object();
            var sw = Stopwatch.StartNew();
            var result = 0.0;
            var count = 0L;
            Parallel.ForEach(Partitioner.Create(0L, chunks * chunkSize), () => (0.0, 0L), (range, state, local) =>
            {
                var samples = range.Item2 - range.Item1;
                var relativeOffset = (range.Item1 + range.Item2) / 2.0 / (chunks * chunkSize);
                var result = new PiCalcuateChunk { RelativeOffset = relativeOffset, Samples = samples }.Calculate();
                return (local.Item1 + result, local.Item2 + 1);
            }, (local) =>
            {
                lock (monitor)
                {
                    result += local.Item1;
                    count += local.Item2;
                }
            });
            sw.Stop();
            return new ProfileReport
            {
                Result = result / count,
                Stopwatch = sw,
            };
        }

        static ProfileReport ParallelNoChunkInline(int chunks, long chunkSize)
        {
            object monitor = new object();
            var sw = Stopwatch.StartNew();
            var result = 0.0;
            var step = 1.0 / (double)(chunks * chunkSize);
            Parallel.ForEach(Partitioner.Create(0L, chunks * chunkSize), () => 0.0, (range, state, local) =>
            {
                for (var i = range.Item1; i < range.Item2; i++)
                {
                    var x = (i + 0.5) * step;
                    var f = 4.0 / (1.0 + x * x);
                    local += f * step;
                }
                return local;
            }, (local) =>
            {
                lock (monitor)
                {
                    result += local;
                }
            });
            sw.Stop();
            return new ProfileReport
            {
                Result = result,
                Stopwatch = sw,
            };
        }

    }
}
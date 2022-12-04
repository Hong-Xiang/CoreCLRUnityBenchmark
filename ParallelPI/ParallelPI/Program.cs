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

namespace ParallelPI
{


    internal class Program
    {
        const int NumberOfSteps = 1_000_000_000;
        const int Chunks = 65536;
        //const int Chunks = 30000;

        /// <summary>Main method to time various implementations of computing PI.</summary>
        static void Main()
        {
            Console.WriteLine("Function               | Elapsed Time     | Estimated Pi         | Chunks / frame (60FPS)");
            Console.WriteLine("-----------------------------------------------------------------");

            //Time(SerialLinqPi, nameof(SerialLinqPi));
            //Time(ParallelLinqPi, nameof(ParallelLinqPi));
            //Time(SerialPi, nameof(SerialPi));
            //Time(ParallelPi, nameof(ParallelPi));
            for (int i = 0; i < 32; i++)
            {
                Time(ParallelPartitionerPi, nameof(ParallelPartitionerPi));
                //Time(ParallelTaskPi, nameof(ParallelTaskPi));
                //Time(ParallelRxPi, nameof(ParallelRxPi));
            }
        }

        /// <summary>Times the execution of a function and outputs both the elapsed time and the function's result.</summary>
        static void Time(
            Func<double> estimatePi,
            string function)
        {
            var sw = Stopwatch.StartNew();
            var pi = estimatePi();
            Console.WriteLine($"{function.PadRight(22)} | {sw.ElapsedMilliseconds,16} | {pi,20} | {Chunks / sw.Elapsed.TotalSeconds / 60}");
        }

        /// <summary>Estimates the value of PI using a LINQ-based implementation.</summary>
        static double SerialLinqPi()
        {
            double step = 1.0 / (double)NumberOfSteps;
            return (from i in Enumerable.Range(0, NumberOfSteps)
                    let x = (i + 0.5) * step
                    select 4.0 / (1.0 + x * x)).Sum() * step;
        }

        /// <summary>Estimates the value of PI using a PLINQ-based implementation.</summary>
        static double ParallelLinqPi()
        {
            double step = 1.0 / (double)NumberOfSteps;
            return (from i in ParallelEnumerable.Range(0, NumberOfSteps)
                    let x = (i + 0.5) * step
                    select 4.0 / (1.0 + x * x)).Sum() * step;
        }

        /// <summary>Estimates the value of PI using a for loop.</summary>
        static double SerialPi()
        {
            double sum = 0.0;
            double step = 1.0 / (double)NumberOfSteps;
            for (int i = 0; i < NumberOfSteps; i++)
            {
                double x = (i + 0.5) * step;
                sum += 4.0 / (1.0 + x * x);
            }
            return step * sum;
        }

        /// <summary>Estimates the value of PI using a Parallel.For.</summary>
        static double ParallelPi()
        {
            double sum = 0.0;
            double step = 1.0 / (double)NumberOfSteps;
            object monitor = new object();
            Parallel.For(0, NumberOfSteps, () => 0.0, (i, state, local) =>
            {
                double x = (i + 0.5) * step;
                return local + 4.0 / (1.0 + x * x);
            }, local => { lock (monitor) sum += local; });
            return step * sum;
        }

        /// <summary>Estimates the value of PI using a Parallel.ForEach and a range partitioner.</summary>
        static double ParallelPartitionerPi()
        {
            double sum = 0.0;
            double step = 1.0 / (double)NumberOfSteps;
            object monitor = new object();
            var chunkSize = (int)Math.Ceiling((float)NumberOfSteps / Chunks);
            Console.WriteLine($"chunks {Chunks}, chunk size {chunkSize}");
            Parallel.ForEach(Partitioner.Create(0, NumberOfSteps, chunkSize), () => 0.0, (range, state, local) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    double x = (i + 0.5) * step;
                    local += 4.0 / (1.0 + x * x);
                }
                return local;
            }, local => { lock (monitor) sum += local; });
            return step * sum;
        }

        class IndexValue
        {
            public int Value { get; set; }
        }

        static double ParallelTaskPi()
        {
            double step = 1.0 / (double)NumberOfSteps;
            var chunkSize = (int)Math.Ceiling((float)NumberOfSteps / Chunks);
            Console.WriteLine($"chunks {Chunks}, chunk size {chunkSize}");
            var tasks = new Task<double>[Chunks];
            var starts = new int[Chunks];
            var ends = new int[Chunks];
            for (int i = 0; i < Chunks; i++)
            {
                tasks[i] = Task.Factory.StartNew((indexObj) =>
                {
                    var index = (indexObj as IndexValue).Value;
                    var start = Math.Min(index * chunkSize, NumberOfSteps - 1);
                    var end = Math.Min((index + 1) * chunkSize, NumberOfSteps);
                    double local = 0.0;
                    for (int i = start; i < end; i++)
                    {
                        double x = (i + 0.5) * step;
                        local += 4.0 / (1.0 + x * x);
                    }
                    return local;
                }, new IndexValue { Value = i });
            }
            return Task.WhenAll(tasks).Result.Sum();
        }

        static double ParallelRxPi()
        {
            var samples = NumberOfSteps;
            double step = 1.0 / (double)samples;
            var chunkSize = (int)Math.Ceiling((float)samples / Chunks);
            Console.WriteLine($"chunk size {chunkSize}");
            var o = Observable.Range(0, Chunks).Select(index => Observable.Start(
                           () =>
                           {
                               var start = Math.Min(index * chunkSize, samples - 1);
                               var end = Math.Min((index + 1) * chunkSize, samples);
                               double local = 0.0;
                               for (int i = start; i < end; i++)
                               {
                                   double x = (i + 0.5) * step;
                                   local += 4.0 / (1.0 + x * x);
                               }
                               return local;
                           })).Merge<double>(12).Sum();
            return o.Wait() / samples;
        }
    }
}
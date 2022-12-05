using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Jobs;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Profiling;

[BurstCompile]
public struct PiJob : IJobParallelFor
{
    public int chunkSize;
    public int totalSamples;
    public double step;
    public NativeArray<double> results;

    [BurstCompile]
    public void Execute(int index)
    {
        var start = math.min(index * chunkSize, totalSamples - 1);
        var end = math.min((index + 1) * chunkSize, totalSamples);
        double local = 0;
        for (int i = start; i < end; i++)
        {
            double x = (i + 0.5) * step;
            local += 4.0 / (1.0 + x * x);
        }
        results[index] = local;
    }
}

public class ParallelPI : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        var totalSamples = 1_000_000_000;
        var chunkSize = 65536;
        var chunks = (int)Mathf.Ceil((float)totalSamples / chunkSize);
        var results = CollectionHelper.CreateNativeArray<double>(chunks, Allocator.TempJob);
        double step = 1.0 / totalSamples;
        var jobData = new PiJob
        {
            chunkSize = chunkSize,
            totalSamples = totalSamples,
            step = step,
            results = results
        };
        Profiler.BeginSample("ParallelPi");
        jobData.Schedule(results.Length, 1).Complete();
        Profiler.EndSample();
        Debug.Log(results.ToArray().Sum() / totalSamples);
        results.Dispose();
    }
}

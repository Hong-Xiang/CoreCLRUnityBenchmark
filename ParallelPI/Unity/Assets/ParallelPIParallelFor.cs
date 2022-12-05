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
public struct PiJobP : IJobParallelFor
{
    public int chunks;
    public long chunkSize;
    public NativeArray<double> results;

    [BurstCompile]
    public void Execute(int chunkIndex)
    {
        results[chunkIndex] = new PiCalcuateChunk { RelativeOffset = (double)chunkIndex / chunks, Samples = chunkSize }.Calculate();
    }
}

public class ParallelPIParallelFor : MonoBehaviour
{
    public int Chunks = 65536;
    public long ChunkSize = 1024;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        var results = CollectionHelper.CreateNativeArray<double>(Chunks, Allocator.TempJob);
        var jobData = new PiJobP
        {
            chunks = Chunks,
            chunkSize = ChunkSize,
            results = results
        };
        Profiler.BeginSample("ParallelPi");
        jobData.Schedule(results.Length, 1).Complete();
        Profiler.EndSample();
        Debug.Log(results.ToArray().Average());
        results.Dispose();
    }
}
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Jobs;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Profiling;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

[BurstCompile]
public struct PiJobS : IJob
{

    public int chunks;
    public long chunkSize;
    public int chunkIndex;
    [NativeDisableContainerSafetyRestriction]
    public NativeArray<double> results;

    [BurstCompile]
    public void Execute()
    {
        results[chunkIndex] = new PiCalcuateChunk { RelativeOffset = (double)chunkIndex / chunks, Samples = chunkSize }.Calculate();
    }
}


public class ParallelPISimpleJob : MonoBehaviour
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
        var jobs = CollectionHelper.CreateNativeArray<JobHandle>(Chunks, Allocator.Temp);
        Profiler.BeginSample("ParallelPi");
        for (int i = 0; i < Chunks; i++)
        {
            jobs[i] = new PiJobS
            {
                chunks = Chunks,
                chunkIndex = i,
                chunkSize = ChunkSize,
                results = results
            }.Schedule();
        }
        JobHandle.CombineDependencies(jobs).Complete();
        Profiler.EndSample();
        Debug.Log(results.ToArray().Average());
        results.Dispose();
        jobs.Dispose();
    }
}

[BurstCompile]
public struct ParallelSimpleJobSystem : ISystem
{
    NativeArray<double> results;
    NativeArray<JobHandle> jobs;
    int Chunks;
    int ChunkSize;
    public void OnCreate(ref SystemState state)
    {
        Chunks = 65536;
        ChunkSize = 128;
        results = CollectionHelper.CreateNativeArray<double>(Chunks, Allocator.Persistent);
        jobs = CollectionHelper.CreateNativeArray<JobHandle>(Chunks, Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        results.Dispose();
        jobs.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        for (int i = 0; i < Chunks; i++)
        {
            jobs[i] = new PiJobS
            {
                chunks = Chunks,
                chunkIndex = i,
                chunkSize = ChunkSize,
                results = results
            }.Schedule();
        }
        JobHandle.CombineDependencies(jobs).Complete();
        double result = 0.0;
        foreach (var x in results)
        {
            result += x;
        }
        Debug.Log(result / results.Length);
    }
}

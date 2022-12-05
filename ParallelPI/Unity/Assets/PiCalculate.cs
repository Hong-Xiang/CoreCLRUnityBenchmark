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
struct PiCalcuateChunk
{
    public long Samples;
    public double RelativeOffset;

    [BurstCompile]
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


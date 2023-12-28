using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Linq;

/*
 * Test which spheres are in the view frustum.
 * I was interested in vector4.dot(pos,norm) vs vector3.dot(pos,norm)+d
 * As well as concurrent solutions
 * 
 * The SIMD implementation started out based on the battlefield talk, however that only used v128. Using v256 makes the implementation easier and more performant, so i went with that.
 * 
 * Takeaways:
 * 
 * - Avoid branching and use &&! This makes a huge difference, as everyone says
 * - SIMD not so scary i think. Not sure how to use the wasted two numbers, i dont *have* 256 bits to add up so what do? Still performs really well
 * - I'm not sure how to pass data to Task.Run, this clojure captures so much. I wouldnt mind making a struct of things, i saw some other methods that had an object as parameter. 
 *      That means boxing and casting and nastyness, but its likely worth it.
 *      
 * A simd and parallel one would be great, i'm sure cpu culling wouldnt be the bottleneck. 
 * 
 * | Method                                | Mean      | Error    | StdDev   |
|-------------------------------------- |----------:|---------:|---------:|
| GetVisibleCount_Simple                | 152.99 us | 1.422 us | 1.330 us |
| GetVisibleCount_Dot                   | 142.66 us | 0.550 us | 0.514 us |
| GetVisibleCount_Dot_Inline            | 127.14 us | 0.130 us | 0.122 us |
| GetVisibleCount_Dot_NoLoop            | 124.51 us | 0.555 us | 0.464 us |
| GetVisibleCount_Dot_Inline_And        |  57.14 us | 0.028 us | 0.025 us |
| GetVisibleCount_Dot_Inline_And_Unsafe |  53.29 us | 0.146 us | 0.130 us |
| GetVisibleCount_Single                |  56.91 us | 0.070 us | 0.062 us |


| GetVisibleCount_SIMD_V256             |  22.36 us | 0.296 us | 0.277 us |
| GetVisibleCount_TaskRun               |  14.06 us | 0.051 us | 0.047 us |
| GetVisibleCount_AsParallel            |  43.12 us | 0.565 us | 0.529 us |
| GetVisibleCount_ParallelFor           |  22.87 us | 0.065 us | 0.061 us |
| GetVisibleCount_ParallelForEach       |  52.49 us | 0.155 us | 0.145 us |
 * 
 * https://www.youtube.com/watch?v=IXE06TlWDgw | https://www.ea.com/frostbite/news/culling-the-battlefield-data-oriented-design-in-practice
 * https://github.com/SungJJinKang/EveryCulling/blob/main/CullingModule/ViewFrustumCulling/ViewFrustumCulling.cpp
 */


public class SphereCullTest
{
    private Plane[] planes;

    readonly struct Renderable
    {
        public Vector3 Position { get; init; }
        public float Radius { get; init; }
    }

    private Renderable[] renderables;


    public SphereCullTest()
    {
        planes = new Plane[6];
        planes[0] = new Plane(Vector3.UnitZ, 0f);   //near
        planes[1] = new Plane(-Vector3.UnitZ, 20f); //far

        planes[2] = new Plane(Vector3.UnitX, 20f);  //left
        planes[3] = new Plane(-Vector3.UnitX, 20f); //right

        planes[4] = new Plane(Vector3.UnitY, 20f);  //bottom
        planes[5] = new Plane(-Vector3.UnitY, 20f); //top

        var rng = new Random(1234);

        renderables = new Renderable[16000];

        for (int i = 0; i < renderables.Length; i++)
        {
            //This double cast is terrible for random
            renderables[i] = new Renderable()
            {
                Position = new Vector3((float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f),
                Radius = (float)rng.NextDouble() * 20f
            };
        }
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public int GetVisibleCount_Simple()
    {
        int count = 0;

        bool isVisible(Renderable r)
        {
            for (int i = 0; i < 6; i++)
            {
                float radius = r.Radius;

                float distanceToPlane = Vector3.Dot(planes[i].Normal, r.Position) + planes[i].D;//Plane.DotCoordinate 
                bool axisVisible = distanceToPlane > -radius;
                if (!axisVisible) return false;
            }
            return true;
        }

        foreach (var r in renderables)
            if (isVisible(r)) count++;
        return count;
    }
    [BenchmarkDotNet.Attributes.Benchmark]
    public int GetVisibleCount_Dot()
    {
        int count = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool isVisible(Renderable r)
        {
            for (int i = 0; i < 6; i++)
            {
                float radius = r.Radius;

                Vector4 re = new Vector4(r.Position, 1f);
                Vector4 pl = new Vector4(planes[i].Normal, planes[i].D);

                float d = Vector4.Dot(re, pl);
                if (d < -radius) return false;
            }
            return true;
        }

        foreach (var r in renderables)
            if (isVisible(r)) count++;
        return count;
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public int GetVisibleCount_Dot_Inline()
    {
        int count = 0;

        for (int i = 0; i < renderables.Length; i++)
        {
            var r = renderables[i];

            Vector4 pos = new Vector4(r.Position, 1f);
            float radius = r.Radius;

            bool visible = true;
            for (int j = 0; j < 6; j++)
            {
                Vector4 pl = Unsafe.As<Plane, Vector4>(ref planes[j]);

                float d = Vector4.Dot(pos, pl);
                if (d < -radius)
                {
                    visible = false;
                    break;
                }
            }
            if (visible) count++;
        }
        return count;
    }


    [BenchmarkDotNet.Attributes.Benchmark]
    public int GetVisibleCount_Dot_NoLoop()
    {
        int count = 0;

        for (int i = 0; i < renderables.Length; i++)
        {
            var r = renderables[i];

            Vector4 pos = new Vector4(r.Position, 1f);
            float radius = r.Radius;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool isVisible(int idx)
            {
                Vector4 pl = Unsafe.As<Plane, Vector4>(ref planes[idx]);
                return Vector4.Dot(pos, pl) + radius > 0;
            }

            bool visible = isVisible(0);
            if (visible) visible = isVisible(1);
            if (visible) visible = isVisible(2);
            if (visible) visible = isVisible(3);
            if (visible) visible = isVisible(4);
            if (visible) visible = isVisible(5);

            if (visible) count++;
        }
        return count;
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public int GetVisibleCount_Dot_Inline_And()
    {
        int count = 0;

        for (int i = 0; i < renderables.Length; i++)
        {
            var r = renderables[i];

            Vector4 pos = new Vector4(r.Position, 1f);
            float radius = r.Radius;

            bool visible = true;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[0])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[1])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[2])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[3])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[4])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[5])) > -radius;

            if (visible) count++;
        }
        return count;
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public int GetVisibleCount_SIMD_V256()
    {
        var planeX = Vector256.Create(
            planes[0].Normal.X,
            planes[1].Normal.X,
            planes[2].Normal.X,
            planes[3].Normal.X,
            planes[4].Normal.X,
            planes[5].Normal.X, 0f, 0f);
        var planeY = Vector256.Create(
            planes[0].Normal.Y,
            planes[1].Normal.Y,
            planes[2].Normal.Y,
            planes[3].Normal.Y,
            planes[4].Normal.Y,
            planes[5].Normal.Y, 0f, 0f);
        var planeZ = Vector256.Create(
            planes[0].Normal.Z,
            planes[1].Normal.Z,
            planes[2].Normal.Z,
            planes[3].Normal.Z,
            planes[4].Normal.Z,
            planes[5].Normal.Z, 0f, 0f);
        var planeD = Vector256.Create(
            planes[0].D,
            planes[1].D,
            planes[2].D,
            planes[3].D,
            planes[4].D,
            planes[5].D, 0f, 0f);

        var comp = Vector256.Create(-1); // Vector256.Create(-1, -1, -1, -1, -1, -1, 0, 0);

        //https://www.youtube.com/watch?v=IXE06TlWDgw&t=1595s

        int count = 0;
        int i;
        //We're testing all view frustrums at once, per renderables. 
        //So we still loop once for each renderable
        for (i = 0; i < renderables.Length; i++)
        {
            /*
             * We're doing:
             * posX * planeX + posY * planeY + posZ * planeZ + planeD > -radius
             * 
             * But actually:
             * d = posx * planeX
             * d = posY * planeY + d;
             * d = posZ * planeZ + d;
             * d = d + planeD;
             * 
             * We're smart and do the addition on planeD in the first statement
             */
            var posA_xxxx = Vector256.Create(renderables[i].Position.X);
            var posA_yyyy = Vector256.Create(renderables[i].Position.Y);
            var posA_zzzz = Vector256.Create(renderables[i].Position.Z);

            var posA_rrrr = Vector256.Create(-renderables[i].Radius);

            var dotA_0123 = Fma.MultiplyAdd(posA_xxxx, planeX, planeD);
            dotA_0123 = Fma.MultiplyAdd(posA_yyyy, planeY, dotA_0123);
            dotA_0123 = Fma.MultiplyAdd(posA_zzzz, planeZ, dotA_0123);

            var res = Avx.CompareGreaterThan(dotA_0123, posA_rrrr).AsInt32();

            if (res == comp)
                count++;
        }

        return count;
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public unsafe int GetVisibleCount_Dot_Inline_And_Unsafe()
    {
        int count = 0;

        //not much difference between this and getreference (https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.memorymarshal.getreference?view=net-8.0)
        //i kinda prefer it not moving around?
        //Not much difference between safe variant anyway
        fixed (Renderable* begin = &renderables[0],
                end = &renderables[^1])
        {
            var r = begin;

            while (r != end)
            {
                Vector4 pos = new Vector4(r->Position, 1f);
                float radius = r->Radius;

                bool visible = true;
                visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[0])) > -radius;
                visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[1])) > -radius;
                visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[2])) > -radius;
                visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[3])) > -radius;
                visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[4])) > -radius;
                visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[5])) > -radius;

                if (visible) count++;

                r++;
            }
        }
        return count;
    }


    [BenchmarkDotNet.Attributes.Benchmark]
    public int GetVisibleCount_Single()
    {
        var planes = this.planes;
        var renderables = this.renderables;

        return getVisibleCount(planes, renderables);
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public int GetVisibleCount_TaskRun()
    {
        var planes = this.planes;
        var renderables = this.renderables;

        int threads = 16;

        //todo not multiple of 16
        int count = renderables.Length / threads;

        var tasks = new Task<int>[threads];
        for (int i = 0; i < threads; i++)
        {
            int j = i; //important, capture this iteration variable. Otherwise in the func has a bad i variable


            tasks[i] = Task.Run(() => getVisibleCount(planes, renderables.AsSpan(j * count, count - 1)));
        }
        Task.WaitAll(tasks);

        int total = 0;
        for (int i = 0; i < tasks.Length; i++)
            total += tasks[i].Result;

        return total;
    }

    private static int getVisibleCount(in Plane[] planes, in ReadOnlySpan<Renderable> renderables)
    {
        int count = 0;

        for (int i = 0; i < renderables.Length; i++)
        {
            var r = renderables[i];

            Vector4 pos = new Vector4(r.Position, 1f);
            float radius = r.Radius;

            bool visible = true;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[0])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[1])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[2])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[3])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[4])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[5])) > -radius;

            if (visible) 
                count++;
        }
        return count;
    }


    [BenchmarkDotNet.Attributes.Benchmark]
    public int GetVisibleCount_AsParallel()
    {
        return renderables.AsParallel().Sum((r) =>
        {
            Vector4 pos = new Vector4(r.Position, 1f);
            float radius = r.Radius;

            bool visible = true;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[0])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[1])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[2])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[3])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[4])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[5])) > -radius;

            return visible ? 1 : 0;
        });

    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public int GetVisibleCount_ParallelFor()
    {
        int total = 0;

        Parallel.For(0, renderables.Length,
            () => 0,  //per-thread data init to zero

            (i, loop, count) =>
        {
            var r = renderables[i];
            Vector4 pos = new Vector4(r.Position, 1f);
            float radius = r.Radius;

            bool visible = true;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[0])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[1])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[2])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[3])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[4])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[5])) > -radius;

            if (visible)
                count++;
            return count;
        },
            (count) => Interlocked.Add(ref total, count));
        return total;
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public int GetVisibleCount_ParallelForEach()
    {
        int total = 0;

        Parallel.ForEach(renderables, (r) =>
        {

            Vector4 pos = new Vector4(r.Position, 1f);
            float radius = r.Radius;

            bool visible = true;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[0])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[1])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[2])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[3])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[4])) > -radius;
            visible = visible && Vector4.Dot(pos, Unsafe.As<Plane, Vector4>(ref planes[5])) > -radius;

            //this bad look here https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/how-to-write-a-parallel-foreach-loop-with-partition-local-variables
            if (visible)
                Interlocked.Add(ref total, 1);
        });
        return total;
    }


}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

/*
 * Based on Computer, Enhance! performance aware programming
 * 
 * It benchmarks adding up the contents of an int[], and shows that adding to 4 different accumulators is much faster. 
 * This is because it breaks a serial dependency chain, if you keep adding into the same variable the CPU cant do these in parallel. 
 * My ryzen 7 laptop CPU is able to do 4 adds per instruction cycle, but *only* if thats not serial/dependant.
 * 
 * I then try the same with SIMD adding, that also shows some improvement but not as dramatic. 
 * My conclusions are its neat non-serial adding is nearly twice as fast.
 * SIMD is 100x as fast, and non-serial-simd slightly faster. 
 * However i dont think the extra effort and maintenance of nonsserial simd, and unsafe/load variants is really worth it
 * 
 * 
| Method                               | Mean      | Error    | StdDev   |
|------------------------------------- |----------:|---------:|---------:|
| TestSimple                           | 496.74 ns | 2.520 ns | 2.357 ns |
| TestSimple_Pair                      | 317.41 ns | 3.979 ns | 3.722 ns |
| TestSimple_Pair_NonSerial            | 374.87 ns | 2.919 ns | 2.731 ns |
| TestSimple_NonSerial_4               | 273.96 ns | 1.282 ns | 1.199 ns |
| TestSimple_NonSerial_8               | 254.35 ns | 0.779 ns | 0.729 ns |
| TestSimple_SIMD                      |  37.93 ns | 0.060 ns | 0.053 ns |
| TestSimple_NonSerial_4_SIMD_Generic  |  29.24 ns | 0.398 ns | 0.372 ns |
| TestSimple_NonSerial_4_SIMD_Avx2     |  31.48 ns | 0.548 ns | 0.513 ns |
| TestSimple_NonSerial_4_SIMD_Load     |  27.46 ns | 0.021 ns | 0.019 ns |
| TestSimple_NonSerial_4_SIMD_UnsafeAs |  19.74 ns | 0.158 ns | 0.148 ns |
 * 
 */
public class AddTest
{
    readonly int[] data;

    public AddTest()
    {
        data = new int[1024];
        var rng = new System.Random();
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = rng.Next(100);
        }
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public int TestSimple()
    {
        int total = 0;
        for (int i = 0; i < data.Length; i++)
        {
            total += data[i];
        }
        return total;
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public int TestSimple_Pair()
    {
        int total = 0;
        for (int i = 0; i < data.Length; i += 2)
        {
            total += data[i];
            total += data[i + 1];
        }
        return total;
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public int TestSimple_Pair_NonSerial()
    {
        int total = 0;
        int totalB = 0;
        for (int i = 0; i < data.Length; i += 2)
        {
            total += data[i];
            totalB += data[i + 1];
        }
        return total;
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public int TestSimple_NonSerial_4()
    {
        int totalA = 0;
        int totalB = 0;
        int totalC = 0;
        int totalD = 0;

        for (int i = 0; i < data.Length; i += 4)
        {
            totalA += data[i];
            totalB += data[i + 1];
            totalC += data[i + 2];
            totalD += data[i + 3];
        }
        return totalA + totalB + totalC + totalD;
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public int TestSimple_NonSerial_8()
    {
        int totalA = 0;
        int totalB = 0;
        int totalC = 0;
        int totalD = 0;
        int totalE = 0;
        int totalF = 0;
        int totalG = 0;
        int totalH = 0;

        for (int i = 0; i < data.Length; i += 8)
        {
            totalA += data[i];
            totalB += data[i + 1];
            totalC += data[i + 2];
            totalD += data[i + 3];
            totalE += data[i + 4];
            totalF += data[i + 5];
            totalG += data[i + 6];
            totalH += data[i + 7];
        }
        return totalA + totalB + totalC + totalD + totalE + totalF + totalG + totalH;
    }


    [BenchmarkDotNet.Attributes.Benchmark]
    public int TestSimple_SIMD()
    {
        var vTotal = Vector256<int>.Zero;

        var vData = MemoryMarshal.Cast<int, Vector256<int>>(data);

        for (int i = 0; i < vData.Length; i++)
        {
            vTotal = Vector256.Add(vTotal, vData[i]);
        }

        return Vector256.Sum(vTotal);
    }


    [BenchmarkDotNet.Attributes.Benchmark]
    public int TestSimple_NonSerial_4_SIMD_Generic()
    {
        var totalA = Vector256<int>.Zero;
        var totalB = Vector256<int>.Zero;
        var totalC = Vector256<int>.Zero;
        var totalD = Vector256<int>.Zero;

        var vData = MemoryMarshal.Cast<int, Vector256<int>>(data);

        for (int i = 0; i < vData.Length; i += 4)
        {
            totalA = Vector256.Add(totalA, vData[i]);
            totalB = Vector256.Add(totalB, vData[i + 1]);
            totalC = Vector256.Add(totalC, vData[i + 2]);
            totalD = Vector256.Add(totalD, vData[i + 3]);
        }
        return Vector256.Sum(totalA) +
            Vector256.Sum(totalB) +
            Vector256.Sum(totalC) +
            Vector256.Sum(totalD);
    }

    [BenchmarkDotNet.Attributes.Benchmark]
    public unsafe int TestSimple_NonSerial_4_SIMD_Avx2()
    {
        var totalA = Vector256<int>.Zero;
        var totalB = Vector256<int>.Zero;
        var totalC = Vector256<int>.Zero;
        var totalD = Vector256<int>.Zero;

        var vData = MemoryMarshal.Cast<int, Vector256<int>>(data);

        for (int i = 0; i < vData.Length; i += 4)
        {
            totalA = Avx2.Add(totalA, vData[i]);
            totalB = Avx2.Add(totalB, vData[i + 1]);
            totalC = Avx2.Add(totalC, vData[i + 2]);
            totalD = Avx2.Add(totalD, vData[i + 3]);
        }

        return Vector256.Sum(totalA) +
            Vector256.Sum(totalB) +
            Vector256.Sum(totalC) +
            Vector256.Sum(totalD);
    }


    [BenchmarkDotNet.Attributes.Benchmark]
    public unsafe int TestSimple_NonSerial_4_SIMD_Load()
    {
        var totalA = Vector256<int>.Zero;
        var totalB = Vector256<int>.Zero;
        var totalC = Vector256<int>.Zero;
        var totalD = Vector256<int>.Zero;

        nint length = data.Length;
        const int v256Count = 8;//Vector256<int>.Count

        fixed (int* begin = data)
        {
            int* cur = begin;

            for (nint i = 0; i < length; i += v256Count * 4)
            {
                totalA += Vector256.Load(begin + i);
                totalB += Vector256.Load(begin + i + v256Count);
                totalC += Vector256.Load(begin + i + v256Count * 2);
                totalD += Vector256.Load(begin + i + v256Count * 3);
            }


        }

        return Vector256.Sum(totalA) +
            Vector256.Sum(totalB) +
            Vector256.Sum(totalC) +
            Vector256.Sum(totalD);
    }


    [BenchmarkDotNet.Attributes.Benchmark]
    public unsafe int TestSimple_NonSerial_4_SIMD_UnsafeAs()
    {
        var totalA = Vector256<int>.Zero;
        var totalB = Vector256<int>.Zero;
        var totalC = Vector256<int>.Zero;
        var totalD = Vector256<int>.Zero;

        ref var pv = ref MemoryMarshal.GetReference(data.AsSpan());

        nint length = data.Length;

        for (nint i = 0; i < length; i += Vector256<int>.Count * 4)
        {
            totalA = totalA + Unsafe.As<int, Vector256<int>>(ref Unsafe.Add(ref pv, i));
            totalB = totalB + Unsafe.As<int, Vector256<int>>(ref Unsafe.Add(ref pv, i + Vector256<int>.Count));
            totalC = totalC + Unsafe.As<int, Vector256<int>>(ref Unsafe.Add(ref pv, i + Vector256<int>.Count * 2));
            totalD = totalD + Unsafe.As<int, Vector256<int>>(ref Unsafe.Add(ref pv, i + Vector256<int>.Count * 3));
        }

        return Vector256.Sum(totalA) +
            Vector256.Sum(totalB) +
            Vector256.Sum(totalC) +
            Vector256.Sum(totalD);
    }
    
}
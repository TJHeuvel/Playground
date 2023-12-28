using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Runtime.Intrinsics.X86;

#if DEBUG

var t = new SphereCullTest();
Console.WriteLine(t.GetVisibleCount_Single());
Console.WriteLine(t.GetVisibleCount_SIMD_V256());

return;

//var a = Vector128.Create(1f, 4f, 8f, 16f);
//var b = Vector128.Create(2f, 3f, 8f, 15f);

//var c = Sse.CompareScalarGreaterThan(a, b);
//var d = Sse.CompareGreaterThan(a, b);

////var t = new AABBCullTest();

////Console.WriteLine(t.GetVisibleCount_Simple());
////Console.WriteLine(t.GetVisibleCount_SignFlip());

//Console.WriteLine(t.GetVisibleCount_TaskRun());
//Console.WriteLine(t.GetVisibleCount_ParallelFor());
//Console.WriteLine(t.GetVisibleCount_Dot_Inline_And());
//Console.WriteLine(t.GetVisibleCount_Dot_Inline_And_SIMD());

#else

BenchmarkDotNet.Running.BenchmarkRunner.Run<SphereCullTest>();
#endif

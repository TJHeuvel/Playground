using System.Numerics;

//https://zeux.io/2009/01/31/view-frustum-culling-optimization-introduction/
//https://fgiesen.wordpress.com/2010/10/17/view-frustum-culling/

/*
 * Test which AABB's are inside the view frustum
 * 
 * I wasnt yet aware what Fabian Giesen wrote about the sign flip
 * Its neat, and speeds up the test considerably. 
 * 
 * I'd like to test further:
 *  - recalc instead of store the sign-per-plane. Memory is slow, computation is fast
 *  - store as bits, we only need 3 bits per plane but currently use 3 32bit floats
 *  - see about fancy hacks to do the abs, like setting the first float bit. Surely mathf.abs does this? 
 * 
| Method                   | Mean      | Error    | StdDev   |
|------------------------- |----------:|---------:|---------:|
| GetVisibleCount_Simple   | 110.21 us | 0.455 us | 0.380 us |
| GetVisibleCount_SignFlip |  68.03 us | 0.249 us | 0.233 us |
*/

public class AABBCullTest
{
    private Plane[] planes;

    readonly struct Renderable
    {
        public Vector3 Position { get; init; }
        public Vector3 Extends { get; init; }
    }

    private Renderable[] renderables;

    public AABBCullTest()
    {
        planes = new Plane[6];
        planes[0] = new Plane(Vector3.UnitZ, 0f);   //near
        planes[1] = new Plane(-Vector3.UnitZ, 20f); //far

        planes[2] = new Plane(Vector3.UnitX, 20f);  //left
        planes[3] = new Plane(-Vector3.UnitX, 20f); //right

        planes[4] = new Plane(Vector3.UnitY, 20f);  //bottom
        planes[5] = new Plane(-Vector3.UnitY, 20f); //top

        var rng = new Random(1234);

        renderables = new Renderable[15000];

        for (int i = 0; i < renderables.Length; i++)
        {
            //This double cast is terrible for random
            renderables[i] = new Renderable()
            {
                Position = new Vector3((float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f),
                Extends = new Vector3((float)rng.NextDouble() * 10f, (float)rng.NextDouble() * 10f, (float)rng.NextDouble() * 10f)
            };
        }
    }


    [BenchmarkDotNet.Attributes.Benchmark]
    public int GetVisibleCount_Simple()
    {
        int count = 0;

        foreach (var r in renderables)
        {
            Vector3 center = r.Position;
            Vector3 ext = r.Extends;
            bool inside(Plane plane)
            {
                var absPlane = new Vector3(MathF.Abs(plane.Normal.X), MathF.Abs(plane.Normal.Y), MathF.Abs(plane.Normal.Z));
                return Vector3.Dot(center, plane.Normal) + Vector3.Dot(ext, absPlane) > -plane.D;
            }

            bool visible = true;
            visible = visible && inside(planes[0]);
            visible = visible && inside(planes[1]);
            visible = visible && inside(planes[2]);
            visible = visible && inside(planes[3]);
            visible = visible && inside(planes[4]);
            visible = visible && inside(planes[5]);

            if (visible) count++;
        }

        return count;
    }
    [BenchmarkDotNet.Attributes.Benchmark]
    public int GetVisibleCount_SignFlip()
    {
        int count = 0;

        Span<Vector3> signFlip = stackalloc Vector3[6];
        for (int i = 0; i < signFlip.Length; i++)
            signFlip[i] = new Vector3(MathF.Sign(planes[i].Normal.X), MathF.Sign(planes[i].Normal.Y), MathF.Sign(planes[i].Normal.Z));

        foreach (var r in renderables)
        {
            Vector3 center = r.Position;
            Vector3 ext = r.Extends;

            bool visible = true;
            visible = visible && Vector3.Dot(center + ext * signFlip[0], planes[0].Normal) > -planes[0].D;
            visible = visible && Vector3.Dot(center + ext * signFlip[1], planes[1].Normal) > -planes[1].D;
            visible = visible && Vector3.Dot(center + ext * signFlip[2], planes[2].Normal) > -planes[2].D;
            visible = visible && Vector3.Dot(center + ext * signFlip[3], planes[3].Normal) > -planes[3].D;
            visible = visible && Vector3.Dot(center + ext * signFlip[4], planes[4].Normal) > -planes[4].D;
            visible = visible && Vector3.Dot(center + ext * signFlip[5], planes[5].Normal) > -planes[5].D;

            if (visible) count++;
        }

        return count;
    }
}
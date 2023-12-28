/*
 * https://ajmmertens.medium.com/building-an-ecs-1-where-are-my-entities-and-components-63d07c7da742
 * 
 * The more i read about how to actually have a performant entity component system, the less i like it.
 * It seems like an overly generic approach, a good match for a generic engine. Something custom however i think would benefit more from ad-hoc solutions, and be far simpler.
 * 
 * Data oriented design seems to discourage generic solutions to *all* problems, and an entity class that tries to describe literally everything seems the opposite of that.
 * 
 * I'm also not so sure about the benefit of adding a component to anything to add functionality. I think that sounds nice in theory, but wont be used much for many projects.
 * A lamppost and a player are very different, its not likely we want to have the same feature for both. Even if both are destructible, and have health, you likely want quite different behaviour.
 * I also fear it suffers from exactly the issues generic code has; rather than thinking about what data *this* entity needs you use a generic component.
 * A lamppost might need a full transformation matrix, however a player will never be scaled, and its rotation is also quite constrained. 
 * Having a system act on the existance of <Transform, RenderMesh> means you'll more quickly use the full fat matrix, which you probably dont need. 
 * 
 * The structural change, archetype change, actually makes sense and its what you mostly want. You want different tables for alive and dead players. 
 * The copy when this happens seems scary and a performance issue, but you do *only* want to iterate over alive players so have to move it out of there. 
 */
class EntityManager
{
    private uint entityCount;
    public EntityId CreateEntity() => new EntityId() { Id = ++entityCount };
    public void FreeEntity(EntityId entity) { }


    private struct ArcheTypesPerComponent<T> where T : IComponent
    {
        public static HashSet<ArcheType> ArcheTypes;
    }
    private Dictionary<EntityId, ArcheType> entityArcheTypes;

    public void AddComponent<T>(EntityId entity) where T : IComponent
    {


        //entityTypes[entity].Add(typeof(T));
    }
    public bool HasComponent<T>(EntityId entity) where T : IComponent => entityArcheTypes[entity].Types.Contains(typeof(T));

    public IEnumerator<KeyValuePair<T0, T1>> Query<T0, T1>() where T0 : IComponent where T1 : IComponent
    {
        var a = ArcheTypesPerComponent<T0>.ArcheTypes;
        var b = ArcheTypesPerComponent<T1>.ArcheTypes;

        HashSet<ArcheType> overlappingTypes = new HashSet<ArcheType>(a);
        overlappingTypes.IntersectWith(b);

        foreach (var t in overlappingTypes)
        {
            for (int i = 0; i < t.ComponentCount; i++)
            {
                yield return new KeyValuePair<T0, T1>(t.Get<T0>(i), t.Get<T1>(i));
            }
        }
    }
}

struct ArcheType
{
    public List<Type> Types;
    public int ComponentCount;
    public unsafe ref T Get<T>(int i) where T : IComponent { throw new System.NotImplementedException(); }
}

struct EntityId
{
    public const uint IdMask = 0x00FFFFFF,
                      GenMask = 0xFF000000;

    public uint Data { get; set; }

    public uint Id { get => Data & IdMask; set => Data = value; }
    public byte Generation { get => (byte)(Data >> 24); }
}


//We cant name these columns, this is a bad idea.
struct StaticArcheType<T0>
{
    public static int Count;

    public static EntityId[] Entities;
    public static T0[] Item1;
}
struct StaticArcheType<T0, T1>
{
    public static int Count;

    public static EntityId[] Entities;
    public static T0[] Item1;
    public static T1[] Item2;
}
struct StaticArcheType<T0, T1, T2>
{
    public static int Count;

    public static EntityId[] Entities;
    public static T0[] Item1;
    public static T1[] Item2;
    public static T2[] Item3;
}



interface IComponent { }

struct Transform : IComponent
{
    public Matrix4x4 TRS;
}

struct Velocity : IComponent
{
    public Vector3 Value;
}

struct Model : IComponent { }

struct Health : IComponent
{
    public float Value;
}

unsafe struct Chunk
{
    public fixed byte Buffer[16 * 1024];

    public unsafe ref T Get<T>(int i) where T : unmanaged
    {
        fixed (byte* begin = Buffer)
        {
            T* bgn = (T*)begin;

            return ref bgn[i];
        }

    }
}



abstract class ComponentSystem
{
    protected ref struct KVP<T0, T1>
    {
        public ref T0 Item1;
        public ref T1 Item2;
    }

    protected delegate void ForEach<T0, T1>(ref T0 item1, ref T1 item2);

    protected void query<T0, T1>(ForEach<T0, T1> callback) where T0 : IComponent
                                                                where T1 : IComponent
    {
        //for (int i = 0; i < ArcheType<T0, T1>.Count; i++)
        //{
        //    callback(ref ArcheType<T0, T1>.Item1[i], ref ArcheType<T0, T1>.Item2[i]);
        //}
    }
}

class PhysicsSystem : ComponentSystem
{
    public void Tick()
    {
        query((ref Transform trans, ref Velocity vel) =>
        {
            trans.TRS.Translation += vel.Value;
        });
    }
}
class RenderSystem : ComponentSystem
{
    public void Tick()
    {
        query<Transform, Model>(drawcall);
    }

    private static void drawcall(ref Transform item1, ref Model item2)
    {
        throw new NotImplementedException();
    }
}

class GameLoop
{
    private PhysicsSystem physics;

    public void Tick()
    {
        physics.Tick();
    }

}

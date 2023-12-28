using System;
using System.Numerics;

/*
 * These are tests on how to store data based on Data Oriented Design patterns.
 * Especially the notion of database tables is what i wanted to write out.
 * 
 * I find it hard to undo years of OO thinking. 
 * The TableAlivePlayers shows how a partial class can be used to define data in multiple classes, but iterating comes from a single place.
 * i.e. the PlayerHealth file can add the health fields.
 * 
 * Dont think this is a good idea though, i think it would be better to use a join-type construction. Where the data is in different classes, but the iterator gets it from those.
 
 * Or let go completely of OO and just have the user get data from all over the place.
 * 
 * I do like Handle, how that can clarify what its pointing to. 
 */ 


/*
 * A bunch of data stored contiguously. In order to achieve this when you remove an element others have to move, hence the indices being unstable. 
 * You shouldnt and cant store indices, those are not exposed. 
 */
class UnstableTable<T0>
{
    public int Count { get; private set; }
    private T0[] column0 = new T0[1024];

    public void Add(T0 value)
    {
        column0[++Count] = value;
    }
    public bool Remove(T0 value)
    {
        int idx = Array.IndexOf(column0, value);
        if (idx < 0) return false;

        //Swap back
        column0[idx] = column0[Count];
        Count--;
        return true;
    }


    public ref struct Enumerable
    {
        private UnstableTable<T0> table;
        private int index;

        public Enumerable(UnstableTable<T0> unstableTable) : this()
        {
            this.table = unstableTable;
            this.index = -1;
        }

        public ref T0 Current => ref table.column0[index];

        public bool MoveNext()
        {
            return index++ < table.Count;
        }
    }

    public Enumerable GetEnumerator() => new Enumerable(this);
}


/// <summary>
/// A table with stable indices. When you remove an element the others dont change
/// The tradeoff is you cant iterate over them. 
/// 
/// Based on https://twitter.com/SebAaltonen/status/1699642058829369843
/// </summary>
class StableTable<T0>
{
    public readonly struct Handle
    {
        public ushort Index { get; init; }
        public ushort Generation { get; init; }
    }
    //maybe inline this? this also grows which isnt really what i want
    private Queue<ushort> freeIndices;

    private ushort[] generations;
    private T0[] column0;

    public StableTable(ushort capacity = 256)
    {
        generations = new ushort[capacity];
        column0 = new T0[capacity];
        freeIndices = new Queue<ushort>(capacity);
        for (ushort i = 0; i < capacity; i++)
            freeIndices.Enqueue(i);
    }

    public T0 this[Handle h]
    {
        get
        {
            throwInvalidHandle(h);

            return column0[h.Index];
        }
        set
        {
            throwInvalidHandle(h);
            column0[h.Index] = value;
        }
    }

    private void throwInvalidHandle(Handle h)
    {
        if (h.Index > column0.Length) throw new System.ArgumentOutOfRangeException(nameof(h.Index));
        if (h.Generation != generations[h.Index]) throw new System.Exception("invalid gen");
    }

    public Handle Add(T0 value)
    {
        var newIdx = freeIndices.Dequeue();
        column0[newIdx] = value;

        return new Handle() { Index = newIdx, Generation = generations[newIdx] };
    }
    public bool Remove(Handle handle)
    {
        if (handle.Index > column0.Length ||
            handle.Generation != generations[handle.Index]) return false;

        throwInvalidHandle(handle);

        generations[handle.Index]++;
        freeIndices.Enqueue(handle.Index);
        return true;
    }

    //Its intentional there is no iterator for this, we dont know what handles are in use
    //I fear that limitation is going to suck from day 1. Its not impossible to add ofc, but just kinda sucky
}



struct Handle<T>
{
    public int Index;
}




partial class TableAlivePlayers : Table<TableAlivePlayers.Row>
{
    public const int Capacity = 128;

    public static string[] PlayerNames = new string[Capacity];

    public static Vector3[] Positions = new Vector3[Capacity];
    public static Vector3[] Velocities = new Vector3[Capacity];

    public partial struct Row : IRow
    {
        public int Index { get; init; }

        public ref string Name => ref PlayerNames[Index];

        public ref Vector3 Position => ref Positions[Index];
        public ref Vector3 Velocity => ref Velocities[Index];
    }
}
//This shows how to add info to an existing table. I think this is a bad idea though
partial class TableAlivePlayers : Table<TableAlivePlayers.Row>
{
    public static float[] Healths = new float[Capacity];

    public partial struct Row : IRow
    {
        public ref float Health => ref Healths[Index];

    }
}

abstract partial class Table<T> where T : IRow, new()
{
    public static int Count;// { get; set; }
    public static Enumeratorable Iterate()
    {
        return new Enumeratorable()
        {
            index = -1,
            capacity = Count
        };
    }


    public struct Enumeratorable
    {
        public int index, capacity;

        public Enumeratorable GetEnumerator() => this;

        public T Current => new T() { Index = index };

        public bool MoveNext()
        {
            index++;
            return index < capacity;
        }
    }


}

interface IRow
{
    public int Index { get; init; }
}


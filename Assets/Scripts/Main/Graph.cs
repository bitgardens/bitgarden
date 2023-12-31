using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

public class Graph : MonoBehaviour
{
    public bool _initialized = false;

    private Dictionary<Position, Directions> _topology = new();
    public Dictionary<Position, BgTile> _meta = new();
    public Dictionary<Position, PathNode> walkables = new();
    public uint[] walls;
    public Position portalPosition;

    public uint[] entities;

    public Entity[] AllEntities;


    public Graph() { }

    public void Initialize(Dictionary<string, string> rawTopology, Dictionary<string, GraphManager.RawMetadataEntry> rawMeta, bool newWave)
    {
        // tirei pq a gnt precisa reiniar para waves
        // if (_initialized)
        //     throw new Exception("Already initialized graph");
        // _initialized = true;
        if (newWave)
        {
            _topology = new();
            _meta = new();
            walkables = new();
        }

        foreach (var rawEntry in rawTopology)
        {
            var pos = Position.FromString(rawEntry.Key);
            var dir = Directions.FromString(rawEntry.Value);
            _topology.Add(pos, dir);
        }

        foreach (var (key, rm) in rawMeta)
        {
            var pos = Position.FromString(key);
            var kind = BgTileKindUtils.FromString(rm.Type);
            _meta.Add(pos, new BgTile(kind, pos));
        }

        IEnumerable<Position> topology = this.WalkableNodes();
        foreach (Position p in topology)
        {
            if (this.GetMeta(p).IsWalkable)
            {
                walkables[p] = new PathNode(this.GetMeta(p));
            }
        }

        entities = new uint[0];
        walls = new uint[0];
        AllEntities = new Entity[0];
    }

    /// <summary>
    /// Adds Entity information to meta.
    /// </summary>
    public void AddEntity(Entity entity, Position pos)
    {
        _meta[pos].Entity = entity;
        if (entity.GetType() == typeof(Turret) || entity.GetType() == typeof(Core)) entities = entities.Append(pos.ToId()).ToArray();
        else if (entity.GetType() == typeof(Wall))
        {
            walls = walls.Append(pos.ToId()).ToArray();
        }
        // AllEntities = AllEntities.Append(entity).ToArray();
    }

    /// <summary>
    /// Returns an enumerable over the walkable neighbors of the given position.
    /// </summary>
    public IEnumerable<Position> WalkableNeighbors(Position p)
    {
        var d = _topology[p];
        if (d.Top()) yield return p.WithY(-1);
        if (d.Right()) yield return p.WithX(1);
        if (d.Bottom()) yield return p.WithY(1);
        if (d.Left()) yield return p.WithX(-1);
    }

    /// <summary>
    /// Returns all the walkable nodes in the topology graph.
    /// </summary>
    public IEnumerable<Position> WalkableNodes()
    {
        return _topology.Keys;
    }

    /// <summary>
    /// Checks if a node is in a valid position and if it doesn't already have an entity.
    /// </summary>
    public bool IsAvailableToBuild(Position pos)
    {
        var tile = _meta[pos];
        return tile.IsWalkable && _meta[pos].Entity == null;
    }

    /// <summary>
    /// Returns the tile at the given position.
    /// </summary>
    public BgTile GetMeta(Position p)
    {
        return _meta[p];
    }

    /// <summary>
    /// Returns an enumerable over all metadata entries, i.e., <code>BgTile</code>s.
    /// </summary>
    public IEnumerable<BgTile> AllMeta()
    {
        return _meta.Values;
    }

}

public struct Directions
{
    public readonly byte BitField;

    public Directions(bool top, bool right, bool bottom, bool left)
    {
        BitField = 0;
        BitField |= Enc(top, 3);
        BitField |= Enc(right, 2);
        BitField |= Enc(bottom, 1);
        BitField |= Enc(left, 0);
    }

    public Directions(byte b)
    {
        BitField = b;
    }

    public bool Top() => Is(3);
    public bool Right() => Is(2);
    public bool Bottom() => Is(1);
    public bool Left() => Is(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Enc(bool b, byte shift)
    {
        var flag = b ? 1 : 0;
        return (byte)(flag << shift);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Is(byte shift)
    {
        return (BitField & (1 << shift)) != 0;
    }

    public override string ToString()
    {
        var s = new StringBuilder(4);
        s.Append(Top() ? "1" : "0");
        s.Append(Right() ? "1" : "0");
        s.Append(Bottom() ? "1" : "0");
        s.Append(Left() ? "1" : "0");
        return s.ToString();
    }

    public static Directions FromString(string s)
    {
        if (s.Length != 4)
        {
            throw new InvalidDataException("Must have length 4");
        }

        return new Directions(
            GetBoolAt(s, 0),
            GetBoolAt(s, 1),
            GetBoolAt(s, 2),
            GetBoolAt(s, 3));
    }

    private static bool GetBoolAt(string s, int pos)
    {
        return s[pos] switch
        {
            '0' => false,
            '1' => true,
            _ => throw new InvalidDataException($"Invalid digit: `{s[pos]}`")
        };
    }
}

/// <summary>
/// Encodes two unsigned short integers (u16) into a single unsigned integer (u32).
///
/// The Row component is placed in the least significant portion.
/// The Col component is placed in the most significant portion.
///
/// See tests for usage examples.
/// </summary>
public readonly struct Position
{
    public ushort X { get; }
    public ushort Y { get; }

    public Position(ushort x, ushort y)
    {
        X = x;
        Y = y;
    }

    public Position WithX(short deltaX)
    {
        return new Position((ushort)(X + deltaX), Y);
    }

    public Position WithY(short deltaY)
    {
        return new Position(X, (ushort)(Y + deltaY));
    }

    public Position WithXY(short deltaY, short deltaX)
    {
        return new Position((ushort)(X + deltaX), (ushort)(Y + deltaY));
    }

    public uint ToId()
    {
        var x = (uint)X << 16;
        return x | Y;
        // var y = (uint)Y << 16;
        // return X | y;
    }

    public static Position FromId(uint id)
    {
        const uint hiMask = 0xFFFF0000;
        var x = (id & hiMask) >> 16;
        var y = id & (~hiMask);
        return new Position((ushort)x, (ushort)y);
    }

    public static Position FromString(string rawId)
    {
        return Position.FromId(uint.Parse(rawId));
    }

    public override string ToString()
    {
        return $"Position(x={X}, y={Y})";
    }
}

// When adding a new variant to this enum, do not forget to
// edit the MapPainter.cs implementation.
public enum BgTileKind
{
    None,
    Grass,
    Gbt,
    Gbb,
    Gbl,
    Gblb,
    Gblt,
    Gbr,
    Gbrb,
    Gbrt,
    Gbilt,
    Gbirt,
    Gbilb,
    Gbirb,
    Road1,
    Road2,
    Road3,
    Road4,
    Road5,
    Road6,
    Road7,
    Road8,
    Road9,
    Road10,
    Snow,
    Sbt,
    Sbb,
    Sbl,
    Sblb,
    Sblt,
    Sbr,
    Sbrb,
    Sbrt,
    Sbilt,
    Sbirt,
    Sbilb,
    Sbirb,
    Tree,
    Tree2,
    Tree3,
    Tree4
}

// Sorry.
static class BgTileKindUtils
{
    public static BgTileKind FromString(string raw)
    {
        return raw switch
        {
            "none" => BgTileKind.None,
            "grass" => BgTileKind.Grass,
            "gbt" => BgTileKind.Gbt,
            "gbb" => BgTileKind.Gbb,
            "gbl" => BgTileKind.Gbl,
            "gblb" => BgTileKind.Gblb,
            "gblt" => BgTileKind.Gblt,
            "gbr" => BgTileKind.Gbr,
            "gbrb" => BgTileKind.Gbrb,
            "gbrt" => BgTileKind.Gbrt,
            "gbilt" => BgTileKind.Gbilt,
            "gbirt" => BgTileKind.Gbirt,
            "gbilb" => BgTileKind.Gbilb,
            "gbirb" => BgTileKind.Gbirb,
            "road1" => BgTileKind.Road1,
            "road2" => BgTileKind.Road2,
            "road3" => BgTileKind.Road3,
            "road4" => BgTileKind.Road4,
            "road5" => BgTileKind.Road5,
            "road6" => BgTileKind.Road6,
            "road7" => BgTileKind.Road7,
            "road8" => BgTileKind.Road8,
            "road9" => BgTileKind.Road9,
            "road10" => BgTileKind.Road10,
            "snow" => BgTileKind.Snow,
            "sbt" => BgTileKind.Sbt,
            "sbb" => BgTileKind.Sbb,
            "sbl" => BgTileKind.Sbl,
            "sblb" => BgTileKind.Sblb,
            "sblt" => BgTileKind.Sblt,
            "sbr" => BgTileKind.Sbr,
            "sbrb" => BgTileKind.Sbrb,
            "sbrt" => BgTileKind.Sbrt,
            "sbilt" => BgTileKind.Sbilt,
            "sbirt" => BgTileKind.Sbirt,
            "sbilb" => BgTileKind.Sbilb,
            "sbirb" => BgTileKind.Sbirb,
            "tree" => BgTileKind.Tree,
            "tree2" => BgTileKind.Tree2,
            "tree3" => BgTileKind.Tree3,
            "tree4" => BgTileKind.Tree4,
            _ => throw new DataException("Invalid string to BgTileKind conversion"),
        };
    }

    public static string ToString(BgTileKind kind)
    {
        return kind switch
        {
            BgTileKind.None => "none",
            BgTileKind.Grass => "grass",
            BgTileKind.Gbt => "gbt",
            BgTileKind.Gbb => "gbb",
            BgTileKind.Gbl => "gbl",
            BgTileKind.Gblb => "gblb",
            BgTileKind.Gblt => "gblt",
            BgTileKind.Gbr => "gbr",
            BgTileKind.Gbrb => "gbrb",
            BgTileKind.Gbrt => "gbrt",
            BgTileKind.Gbilt => "gbilt",
            BgTileKind.Gbirt => "gbirt",
            BgTileKind.Gbilb => "gbilb",
            BgTileKind.Gbirb => "gbirb",
            BgTileKind.Road1 => "road1",
            BgTileKind.Road2 => "road2",
            BgTileKind.Road3 => "road3",
            BgTileKind.Road4 => "road4",
            BgTileKind.Road5 => "road5",
            BgTileKind.Road6 => "road6",
            BgTileKind.Road7 => "road7",
            BgTileKind.Road8 => "road8",
            BgTileKind.Road9 => "road9",
            BgTileKind.Road10 => "road10",
            BgTileKind.Snow => "snow",
            BgTileKind.Sbt => "sbt",
            BgTileKind.Sbb => "sbb",
            BgTileKind.Sbl => "sbl",
            BgTileKind.Sblb => "sblb",
            BgTileKind.Sblt => "sblt",
            BgTileKind.Sbr => "sbr",
            BgTileKind.Sbrb => "sbrb",
            BgTileKind.Sbrt => "sbrt",
            BgTileKind.Sbilt => "sbilt",
            BgTileKind.Sbirt => "sbirt",
            BgTileKind.Sbilb => "sbilb",
            BgTileKind.Sbirb => "sbirb",
            BgTileKind.Tree => "tree",
            BgTileKind.Tree2 => "tree2",
            BgTileKind.Tree3 => "tree3",
            BgTileKind.Tree4 => "tree4",
            _ => throw new DataException("unknown BgTileKind"),
        };
    }
}

public class BgTile
{
    public BgTileKind Kind { get; set; }

    // This is not serialized in the JSON representation.
    public Position Position { get; set; }

    public Entity Entity { get; set; }

    public bool IsWalkable { get; set; }
    public int Weight { get; set; }

    public BgTile(BgTileKind kind, Position position)
    {
        Kind = kind;
        Position = position;
        Entity = null;
        if (kind == BgTileKind.Grass || kind == BgTileKind.Road1 || kind == BgTileKind.Road2
        || kind == BgTileKind.Road3 || kind == BgTileKind.Road4 || kind == BgTileKind.Road5
        || kind == BgTileKind.Road6 || kind == BgTileKind.Road7 || kind == BgTileKind.Road8
        || kind == BgTileKind.Road9 || kind == BgTileKind.Road10 || kind == BgTileKind.Snow || kind == BgTileKind.Tree || kind == BgTileKind.Tree2 || kind == BgTileKind.Tree3 || kind == BgTileKind.Tree4)
        {
            IsWalkable = true;
        }
        else
        {
            IsWalkable = false;
        }
        Weight = 1;
    }

    public override string ToString()
    {
        return $"Tile(kind={Kind}, position={Position})";
    }
}
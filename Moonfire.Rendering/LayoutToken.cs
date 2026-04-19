using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.IO.Hashing;
using Moonfire.Rendering.Models;
using Moonfire.Rendering.Interfaces;

namespace Moonfire.Rendering;

public class LayoutToken(IEnumerable<IMoonfireView> views)
{
    public readonly UInt128 Hash = HashLayout(views);

    private readonly ImmutableArray<IMoonfireView> _views = [.. views];
    private ImmutableArray<RegionBorrow>? map;
    
    private void InvalidLayout() => 
        throw new($"Layout map attempted with overlapping regions.\n(X,Y) | (W,H)\n{string.Join('\n', _views.Select(v => $"({v.OriginX},{v.OriginY}) | ({v.SizeX},{v.SizeY})"))}");

    public void MapRegionsHorizontalSweep()
    {
        map = _views.OrderBy(e => e.OriginX).Select(e => new RegionBorrow(new(e.OriginX, e.OriginY, e.SizeX, e.SizeY), e)).ToImmutableArray();

        for(var i = 0; i < map.Value.Length; i++)
        {
            for(var j = i + 1; j < map.Value.Length; j++)
            {
                var r1 = map.Value[i].Region;
                var r2 = map.Value[j].Region;

                if(r2.Y >= r1.Y + r1.H)
                    continue; //Early Exit

                //AABB collision check
                if(r1.X < r2.X + r2.W && r1.X + r1.W > r2.X && r1.Y < r2.Y + r2.H && r1.Y + r1.H > r2.Y)
                    InvalidLayout();
            }
        }
    }
    public void MapRegionsVerticalSweep()
    {
        map = _views.OrderBy(e => e.OriginY).Select(e => new RegionBorrow(new(e.OriginX, e.OriginY, e.SizeX, e.SizeY), e)).ToImmutableArray();

        for(var i = 0; i < map.Value.Length; i++)
        {
            for(var j = i + 1; j < map.Value.Length; j++)
            {
                var r1 = map.Value[i].Region;
                var r2 = map.Value[j].Region;

                if(r2.Y >= r1.Y + r1.H)
                    continue; //Early Exit

                //AABB collision check
                if(r1.X < r2.X + r2.W && r1.X + r1.W > r2.X && r1.Y < r2.Y + r2.H && r1.Y + r1.H > r2.Y)
                    InvalidLayout();
            }
        }
    }
    public async Task Draw(TerminalContext context)
    {
        if(map is null)
            throw new("Attempted Draw on unmapped Layout");

        var tasks = new Task[map.Value.Length];

        for(int i = 0; i < map.Value.Length; i++)
        {
            var e = map.Value[i];
            var r = e.Region;

            tasks[i] = e.View.Draw(new(r.X, r.Y, r.W, r.H, context));
        }

        await Task.WhenAll(tasks);
    }

    public bool Equals(LayoutToken other) => Hash == other.Hash;
    public override bool Equals(object? obj) => obj is LayoutToken other && Equals(other);
    public override int GetHashCode() => Hash.GetHashCode();
    public static bool operator == (LayoutToken left, LayoutToken right) => left.Hash == right.Hash;
    public static bool operator != (LayoutToken left, LayoutToken right) => left.Hash != right.Hash;

    private static UInt128 HashLayout(IEnumerable<IMoonfireView> views)
    {
        var hasher = new XxHash128();
        Span<byte> buffer = new byte[16];

        BinaryPrimitives.WriteInt32LittleEndian(buffer, RuntimeHelpers.GetHashCode(views));
        
        foreach(var view in views)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], RuntimeHelpers.GetHashCode(view));
            BinaryPrimitives.WriteInt16LittleEndian(buffer[8..], (short)view.OriginX);
            BinaryPrimitives.WriteInt16LittleEndian(buffer[10..], (short)view.OriginY);
            BinaryPrimitives.WriteInt16LittleEndian(buffer[12..], (short)view.SizeX);
            BinaryPrimitives.WriteInt16LittleEndian(buffer[14..], (short)view.SizeY);

            hasher.Append(buffer);
        }

        return hasher.GetCurrentHashAsUInt128();
    }

    private readonly struct RegionBorrow(Region region, IMoonfireView view)
    {
        public readonly Region Region = region;
        public readonly IMoonfireView View = view;
    }

    private readonly struct Region(int x, int y, int w, int h)
    {
        public readonly int X = x;
        public readonly int Y = y;
        public readonly int W = w;
        public readonly int H = h;
    }
}

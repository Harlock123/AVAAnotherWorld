namespace AVAAnotherWorld.Engine;

/// <summary>The screen: one room at a time, 1024×768, with nothing over it (no score, no lives, no bars).</summary>
public static class Field
{
    public const double Step = 1.0 / 60;
    public const double Width = 1024, Height = 768;
}

/// <summary>
/// A floor to stand on, from <see cref="X0"/> to <see cref="X1"/>, its top running from <see cref="Y0"/> to <see cref="Y1"/>
/// (a slope if they differ). <see cref="Thick"/> is how deep it is drawn (0: solid rock down to the bottom).
/// </summary>
public sealed record Ledge(double X0, double X1, double Y0, double Y1, double Thick = 0)
{
    public Ledge(double x0, double x1, double y) : this(x0, x1, y, y) { }
    public double YAt(double x) => X1 <= X0 ? Y0 : Y0 + (Y1 - Y0) * Math.Clamp((x - X0) / (X1 - X0), 0, 1);
    public bool Under(double x) => x >= X0 && x <= X1;
}

/// <summary>Something upright in the way, from <see cref="Top"/> down to <see cref="Bottom"/>: a log, a rock, a door.</summary>
public sealed record Wall(double X, double Top, double Bottom);

/// <summary>A rectangle of the room: water, rock, a switch panel.</summary>
public readonly record struct Zone(double X0, double Y0, double X1, double Y1)
{
    public bool Contains(double x, double y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;
    /// <summary>Whether a circle of radius <paramref name="r"/> around a point touches it.</summary>
    public bool Touches(double x, double y, double r)
    {
        double dx = Math.Max(Math.Max(X0 - x, 0), x - X1), dy = Math.Max(Math.Max(Y0 - y, 0), y - Y1);
        return dx * dx + dy * dy < r * r;
    }
}

/// <summary>An energy barrier across the way: on for <see cref="On"/> seconds of every <see cref="Period"/>; or, if switched,
/// on until its switch panel is shot.</summary>
public sealed record Barrier(double X, double Period, double On, double Offset, bool Switched = false);

/// <summary>How a room looks: which backdrop the screen paints behind it.</summary>
public enum Look
{
    Clearing, Pool, Leeches, Cliff, Hall, Cells, Gate, ShaftTop, ShaftMid, Grotto, Shore, Sunken, Landing, Plant, Switchroom,
    Bridge, Span, Tower,
}

/// <summary>A room: one screen, its floors, walls and water, and which rooms are beyond its edges.</summary>
public sealed class Room
{
    public Look Look;
    public List<Ledge> Floors = [];
    public List<Wall> Walls = [];
    public Zone? Water;
    /// <summary>Rock under the water: a swimmer can't pass it (and has no air under it).</summary>
    public List<Zone> Rocks = [];
    public int Left = -1, Right = -1, Down = -1;
    public List<Barrier> Barriers = [];
    /// <summary>Where acid drips from the roof.</summary>
    public List<double> Drips = [];
    /// <summary>A vine hanging from above, down to the point given.</summary>
    public (double X, double Tip)? Vine;
    /// <summary>The switch for the room's switched barrier: a shot turns it off a while.</summary>
    public Zone? Panel;
    /// <summary>A door, shut until the friend opens it.</summary>
    public double? Door;
    /// <summary>The pool's tentacles live at the bottom of the water.</summary>
    public bool Tentacles;

    public Ledge? FloorAt(double x, double y, double slack = 14)
    {
        Ledge? best = null;
        foreach (var l in Floors)
            if (l.Under(x) && Math.Abs(l.YAt(x) - y) <= slack && (best == null || Math.Abs(l.YAt(x) - y) < Math.Abs(best.YAt(x) - y))) best = l;
        return best;
    }
}

/// <summary>Someone in a section at its start: a guard waiting or walking in, the warden, a leech, the beast.</summary>
public sealed record FoeDef(Kind Kind, int Room, double X, double Y, int Facing, double Skill = 0.4, double Gap = 1.2,
    double Wake = 0, double To = double.NaN, bool Shielded = false);

/// <summary>A section of the game: its rooms, where the hero starts (its checkpoint), who's there, and its code.</summary>
public sealed class SectionDef
{
    public int Number;
    public string Name = "";
    /// <summary>The four letters that bring the player back to this checkpoint.</summary>
    public string Code = "";
    public List<Room> Rooms = [];
    public int StartRoom;
    public double StartX, StartY;
    public int Facing = 1;
    public Act StartAct = Act.Stand;
    public bool Gun;
    public List<FoeDef> Foes = [];
    /// <summary>The friend, if he's along.</summary>
    public (int Room, double X)? Friend;
    public SceneId? Before, After;
    public Sfx Ambience = Sfx.Wind;
}

/// <summary>
/// The nine sections from the pool to the tower, each a checkpoint with its code. They are made by hand: rooms side by
/// side (or one above another) with the floors, water, vines, barriers and drips placed where each puzzle needs them.
/// </summary>
public static class Sections
{
    public const int Count = 9;

    private static readonly string[] Codes = ["VXQA", "MRWZ", "QOPE", "ZYNU", "WEMO", "PIRA", "SUVE", "NAYO", "OKAL"];
    private static readonly SectionDef?[] Built = new SectionDef?[Count];
    private static readonly Lock Gate = new();

    public static SectionDef Get(int n)
    {
        lock (Gate) return Built[n - 1] ??= Build(n);
    }

    public static string CodeOf(int n) => Codes[n - 1];

    /// <summary>The section a code brings back, or 0 for a code that's no code.</summary>
    public static int ByCode(string code) => Array.IndexOf(Codes, code.Trim().ToUpperInvariant()) + 1;

    public static SectionDef Build(int n)
    {
        var s = n switch
        {
            1 => Pool(),
            2 => Chase(),
            3 => CageHall(),
            4 => Cells(),
            5 => Shaft(),
            6 => Sunken(),
            7 => Plant(),
            8 => Bridge(),
            9 => Tower(),
            _ => throw new ArgumentOutOfRangeException(nameof(n)),
        };
        s.Number = n;
        s.Code = Codes[n - 1];
        return s;
    }

    // ───────────────────────────── the wilds: sections 1 and 2 ─────────────────────────────

    /// <summary>The pool's surface; the floor of the leech run; the clearing and the pool's banks.</summary>
    public const double Surface = 470, Ground = 600, LogX = 860, VineX = 512, CliffEdge = 800;

    /// <summary>The four rooms of the wilds, left to right: the clearing, the pool, the leech run, the cliff.</summary>
    private static List<Room> Wilds(bool clearing)
    {
        var glade = new Room { Look = Look.Clearing, Floors = [new Ledge(0, 1024, Surface)], Right = 1 };
        var pool = new Room
        {
            Look = Look.Pool,
            Floors = [new Ledge(0, 300, Surface), new Ledge(724, 1024, Surface, Ground)],
            Water = new Zone(300, Surface, 724, 768),
            Vine = (VineX, 330),
            Tentacles = true,
            Left = clearing ? 0 : -1,
            Right = 2,
        };
        var leeches = new Room { Look = Look.Leeches, Floors = [new Ledge(0, 1024, Ground)], Walls = [new Wall(LogX, Ground - 34, Ground)], Left = 1, Right = 3 };
        var cliff = new Room { Look = Look.Cliff, Floors = [new Ledge(0, CliffEdge, Ground)], Left = 2 };
        return [glade, pool, leeches, cliff];
    }

    private static SectionDef Pool() => new()
    {
        Name = "THE POOL",
        Rooms = Wilds(clearing: false),
        StartRoom = 1, StartX = 520, StartY = 715, StartAct = Act.Swim,
        Foes =
        [
            new FoeDef(Kind.Leech, 2, 420, Ground, -1),
            new FoeDef(Kind.Leech, 2, 520, Ground, -1),
            // Hanging from the roof: it drops when the hero comes under it.
            new FoeDef(Kind.Leech, 2, 700, Sim.LeechRoof, -1, To: 640),
        ],
        Before = SceneId.Intro,
        Ambience = Sfx.Wind,
    };

    private static SectionDef Chase() => new()
    {
        Name = "THE BEAST",
        Rooms = Wilds(clearing: true),
        StartRoom = 3, StartX = 660, StartY = Ground, Facing = 1,
        Foes =
        [
            new FoeDef(Kind.Beast, 3, 900, 760, -1, Wake: 1.0),
            new FoeDef(Kind.Leech, 2, 560, Sim.LeechRoof, -1, To: 640),
        ],
        After = SceneId.Captured,
        Ambience = Sfx.Wind,
    };

    // ───────────────────────────── the cage ─────────────────────────────

    public const double HallFloor = 640, CageX = 512, CageTop = 30, Chain = 300;

    private static SectionDef CageHall() => new()
    {
        Name = "THE CAGE",
        Rooms = [new Room { Look = Look.Hall, Floors = [new Ledge(0, 1024, HallFloor)] }],
        StartRoom = 0, StartX = CageX, StartY = CageTop + Chain, StartAct = Act.Caged,
        Foes = [new FoeDef(Kind.Guard, 0, 300, HallFloor, 1, Gap: 1.0)],
        After = SceneId.Freed,
        Ambience = Sfx.Hall,
    };

    // ───────────────────────────── the cells ─────────────────────────────

    public const double CellFloor = 620, DoorX = 930, PanelX = 850;

    private static SectionDef Cells() => new()
    {
        Name = "THE CELLS",
        Rooms =
        [
            new Room { Look = Look.Cells, Floors = [new Ledge(0, 1024, CellFloor)], Right = 1 },
            new Room { Look = Look.Gate, Floors = [new Ledge(0, 1024, CellFloor)], Left = 0, Door = DoorX },
        ],
        StartRoom = 0, StartX = 170, StartY = CellFloor, Gun = true,
        Friend = (0, 80),
        Foes =
        [
            new FoeDef(Kind.Guard, 0, 1080, CellFloor, -1, Skill: 0.3, Gap: 1.3, Wake: 1.2, To: 790),
            new FoeDef(Kind.Guard, 1, -60, CellFloor, 1, Skill: 0.45, Gap: 1.2, Wake: 1.6, To: 210),
            new FoeDef(Kind.Guard, 1, -60, CellFloor, 1, Skill: 0.45, Gap: 1.2, Wake: 5.0, To: 120),
        ],
        Ambience = Sfx.Hall,
    };

    // ───────────────────────────── the shaft and the grotto ─────────────────────────────

    public const double ShaftFloor = 520, StepFloor = 690, GrottoFloor = 660, DripRoof = 140;

    private static SectionDef Shaft() => new()
    {
        Name = "THE SHAFT",
        Rooms =
        [
            new Room { Look = Look.ShaftTop, Floors = [new Ledge(0, 560, ShaftFloor), new Ledge(560, 700, StepFloor, StepFloor, 46)], Down = 1 },
            new Room
            {
                Look = Look.ShaftMid,
                Floors = [new Ledge(680, 880, 130, 130, 40), new Ledge(380, 700, 400, 400, 40), new Ledge(0, 420, GrottoFloor), new Ledge(880, 1024, 700)],
                Left = 2,
            },
            new Room { Look = Look.Grotto, Floors = [new Ledge(0, 1024, GrottoFloor)], Right = 1, Drips = [760, 600, 440] },
        ],
        StartRoom = 0, StartX = 120, StartY = ShaftFloor, Gun = true,
        Foes = [new FoeDef(Kind.Guard, 2, 150, GrottoFloor, 1, Skill: 0.5, Gap: 1.1, Shielded: true)],
        Ambience = Sfx.Cave,
    };

    // ───────────────────────────── the sunken way ─────────────────────────────

    public const double Shore = 300, WaterTop = 330;

    private static SectionDef Sunken() => new()
    {
        Name = "THE SUNKEN WAY",
        Rooms =
        [
            new Room
            {
                Look = Look.Shore, Floors = [new Ledge(0, 240, Shore)], Water = new Zone(240, WaterTop, 1024, 768),
                Rocks = [new Zone(0, Shore, 240, 768), new Zone(420, 0, 1024, 400), new Zone(240, 700, 1024, 768), new Zone(600, 400, 700, 560)],
                Right = 1,
            },
            new Room
            {
                Look = Look.Sunken, Water = new Zone(0, WaterTop, 1024, 768),
                Rocks = [new Zone(0, 0, 450, 400), new Zone(620, 0, 1024, 400), new Zone(0, 690, 1024, 768), new Zone(800, 400, 880, 600)],
                Left = 0, Right = 2,
            },
            new Room
            {
                Look = Look.Landing, Floors = [new Ledge(760, 1024, Shore)], Water = new Zone(0, WaterTop, 1024, 768),
                Rocks = [new Zone(0, 0, 640, 400), new Zone(0, 690, 1024, 768), new Zone(760, Shore, 1024, 768)],
                Left = 1,
            },
        ],
        StartRoom = 0, StartX = 100, StartY = Shore, Gun = true,
        Ambience = Sfx.Cave,
    };

    // ───────────────────────────── the power halls ─────────────────────────────

    public const double PlantFloor = 620;

    private static SectionDef Plant() => new()
    {
        Name = "THE POWER HALLS",
        Rooms =
        [
            new Room { Look = Look.Plant, Floors = [new Ledge(0, 1024, PlantFloor)], Right = 1, Barriers = [new Barrier(380, 2.4, 1.4, 0), new Barrier(660, 2.4, 1.4, 1.2)] },
            new Room
            {
                Look = Look.Switchroom, Floors = [new Ledge(0, 1024, PlantFloor)], Left = 0,
                Barriers = [new Barrier(560, 0, 0, 0, Switched: true)], Panel = new Zone(988, PlantFloor - 120, 1024, PlantFloor - 30),
            },
        ],
        StartRoom = 0, StartX = 100, StartY = PlantFloor, Gun = true,
        Foes = [new FoeDef(Kind.Guard, 1, 860, PlantFloor, -1, Skill: 0.45, Gap: 1.1)],
        Ambience = Sfx.Machine,
    };

    // ───────────────────────────── the bridge ─────────────────────────────

    public const double Deck = 560, GapFrom = 430, GapTo = 640;

    private static SectionDef Bridge() => new()
    {
        Name = "THE BRIDGE",
        Rooms =
        [
            new Room { Look = Look.Bridge, Floors = [new Ledge(0, GapFrom, Deck, Deck, 60), new Ledge(GapTo, 1024, Deck, Deck, 60)], Right = 1 },
            new Room { Look = Look.Span, Floors = [new Ledge(0, 1024, Deck, Deck, 60)], Left = 0 },
        ],
        StartRoom = 0, StartX = 80, StartY = Deck, Gun = true,
        Foes =
        [
            new FoeDef(Kind.Guard, 1, 800, Deck, -1, Skill: 0.5, Gap: 1.0),
            new FoeDef(Kind.Guard, 1, -60, Deck, 1, Skill: 0.5, Gap: 1.1, Wake: 3.0, To: 140),
        ],
        After = SceneId.Reunion,
        Ambience = Sfx.Wind,
    };

    // ───────────────────────────── the tower ─────────────────────────────

    public const double TowerFloor = 600;

    private static SectionDef Tower() => new()
    {
        Name = "THE TOWER",
        Rooms = [new Room { Look = Look.Tower, Floors = [new Ledge(0, 1024, TowerFloor)] }],
        StartRoom = 0, StartX = 120, StartY = TowerFloor, Gun = true,
        Foes = [new FoeDef(Kind.Warden, 0, 840, TowerFloor, -1, Skill: 0.6, Gap: 1.1, Shielded: true)],
        After = SceneId.Ending,
        Ambience = Sfx.Hall,
    };
}

namespace AVAAnotherWorld.Engine;

/// <summary>
/// The computer player, for the demonstration and the tests. It knows each section's way through: swim up out of
/// the pool, kick the leeches, run from the beast and leap for the vine, rock the cage, walk the ledges down the shaft,
/// swim the sunken way by the air pocket, slip through the barriers, leap the broken bridge. Facing a guard it raises a
/// shield, then charges a super shot through his. Where timing matters (the cage, the drips, the barriers) it plays the
/// next moment out on a copy of the world first and waits until it's safe.
/// </summary>
public sealed class Pilot
{
    /// <summary>The charge it's holding fire for, or -1.</summary>
    private double _hold = -1;
    private int _pressTicks, _waypoint, _waypointRoom = -1;

    public string Doing { get; private set; } = "";

    public void Reset()
    {
        _hold = -1;
        _waypoint = 0;
        _waypointRoom = -1;
        Doing = "";
    }

    public Controls Decide(GameEngine g) => g.Phase == Phase.Play ? Decide(g.World) : default;

    public Controls Decide(World w)
    {
        var h = w.Hero;
        if (!h.Alive || w.Done) return default;
        if (_hold >= 0)
        {
            // Holding fire until the charge is there, then letting go.
            if (h.Holding && h.Hold >= _hold) { _hold = -1; return default; }
            if (h.Holding || ++_pressTicks <= 3) return new Controls(Fire: true);
            _hold = -1;
        }
        if (Fight(w) is { } fight) return fight;
        return w.Section switch
        {
            1 => Pool(w),
            2 => Chase(w),
            3 => Cage(w),
            4 => Cells(w),
            5 => Shaft(w),
            6 => Sunken(w),
            7 => Plant(w),
            8 => Bridge(w),
            9 => Wait(w, "facing the warden"),
            _ => default,
        };
    }

    private Controls Hold(double seconds, string doing)
    {
        _hold = seconds;
        _pressTicks = 0;
        Doing = doing;
        return new Controls(Fire: true);
    }

    private Controls Go(int dx, string doing, bool run = false, bool up = false)
    {
        Doing = doing;
        return new Controls(dx, Up: up, Run: run);
    }

    private Controls Wait(World w, string doing)
    {
        Doing = doing;
        return default;
    }

    // ───────────────────────────── a gunfight ─────────────────────────────

    /// <summary>The nearest guard on the hero's floor and on the screen.</summary>
    public static Body? Foe(World w)
    {
        var h = w.Hero;
        return w.Foes.Where(f => f.Alive && f.Kind is Kind.Guard or Kind.Warden && f.Room == h.Room && f.X > -10 && f.X < Field.Width + 10 && Math.Abs(f.Y - h.Y) < 40)
            .OrderBy(f => Math.Abs(f.X - h.X)).FirstOrDefault();
    }

    /// <summary>Shield first, then a super shot through his shield.</summary>
    private Controls? Fight(World w)
    {
        var h = w.Hero;
        if (!h.Gun || !h.OnGround || w.Section == 3 || Foe(w) is not { } foe) return null;
        int toward = Math.Sign(foe.X - h.X);
        if (toward != 0 && toward != h.Facing) return Go(toward, "turning to fight");
        if (h.Act is Act.Turn or Act.Land) return Wait(w, "steadying");
        bool covered = w.Shields.Any(s => s.Side == Side.Hero && s.Room == h.Room && (s.X - h.X) * toward > 0 && (foe.X - s.X) * toward > 0 && s.Life > 2.5);
        return covered ? Hold(Sim.SuperHold + 0.1, "a super shot") : Hold(Sim.ShieldHold + 0.25, "a shield");
    }

    /// <summary>Plays the controls out on a copy of the world: is the hero still alive after so many steps?</summary>
    public static bool Survives(World w, Controls c, int steps)
    {
        var copy = w.Copy();
        for (int i = 0; i < steps; i++)
        {
            Sim.Step(copy, c);
            if (!copy.Hero.Alive) return false;
            if (copy.Done) return true;
        }
        return true;
    }

    /// <summary>The first of the choices that's safe for a while (or the last, if none is).</summary>
    private Controls Safest(World w, int steps, string doing, params Controls[] choices)
    {
        foreach (var c in choices)
            if (Survives(w, c, steps))
            {
                Doing = c == default ? doing + ": waiting" : doing;
                return c;
            }
        Doing = doing + ": nowhere safe";
        return choices[^1];
    }

    // ───────────────────────────── the sections ─────────────────────────────

    private Controls Pool(World w)
    {
        var h = w.Hero;
        if (h.Act == Act.Swim) return Go(1, "swimming up and out", up: true);
        if (h.Room != 2) return Go(1, "walking on");
        // A leech crawling up: let it come, and kick it.
        var leech = w.Foes.Where(f => f.Kind == Kind.Leech && f.Alive && f.Room == 2 && f.Act == Act.Walk && (f.X - h.X) * h.Facing > 0 && (f.X - h.X) * h.Facing < 140)
            .OrderBy(f => Math.Abs(f.X - h.X)).FirstOrDefault();
        if (leech != null)
        {
            if (h.Act == Act.Kick) return Wait(w, "kicking");
            if (Math.Abs(leech.X - h.X) < Sim.KickReach - 4) { Doing = "kicking a leech"; return h.Holding ? default : new Controls(Fire: true); }
            return Wait(w, "waiting for a leech");
        }
        // Under the leech on the roof at a run, and over the log.
        if (h.X > Sections.LogX - 104 && h.X < Sections.LogX - 60 && h.Act == Act.Run) return Go(1, "over the log", run: true, up: true);
        return h.X > 520 ? Go(1, "running under the roof", run: true) : Go(1, "walking on");
    }

    private Controls Chase(World w)
    {
        var h = w.Hero;
        if (h.Room == 2 && h.Act == Act.Run && h.X > Sections.LogX + 70 && h.X < Sections.LogX + 112) return Go(-1, "over the log", run: true, up: true);
        if (h.Room == 1 && h.Act == Act.Run && h.X <= 750 && h.X > 724) return Go(-1, "leaping for the vine", run: true, up: true);
        return Go(-1, "running from the beast", run: true);
    }

    private Controls Cage(World w)
    {
        var cg = w.Cage;
        if (cg.Broken) return Wait(w, "falling");
        if (cg.Peak < 0.45 || Lands(w)) return Go(Rock(w.Cage), cg.Peak < 0.45 ? "rocking the cage" : "rocking it off the chain");
        return Wait(w, "waiting for the guard");
    }

    private static int Rock(Cage cg) => cg.Omega >= 0 ? 1 : -1;

    /// <summary>Rocking on from now: does the cage come down on the guard?</summary>
    private static bool Lands(World w)
    {
        var copy = w.Copy();
        for (int i = 0; i < 60 * 4 && !copy.Cage.Landed; i++) Sim.Step(copy, new Controls(Rock(copy.Cage)));
        return copy.Cage.Crushed;
    }

    private Controls Cells(World w)
    {
        var h = w.Hero;
        if (h.Room == 0) return Go(1, "along the cells");
        bool coming = w.Foes.Any(f => f.Alive && f.Room == 1);
        if (!coming && w.DoorOpen) return Go(1, "through the door");
        if (h.X < 480) return Go(1, "to the middle of the room");
        if (coming && h.Facing == 1) return Go(-1, "turning to meet them");
        bool covered = w.Shields.Any(s => s.Side == Side.Hero && s.Room == 1 && s.X < h.X && s.Life > 3);
        if (coming && !covered && h.OnGround && h.Act is not (Act.Turn or Act.Land)) return Hold(Sim.ShieldHold + 0.25, "a shield before they come");
        return Wait(w, "holding the door");
    }

    private Controls Shaft(World w)
    {
        var h = w.Hero;
        if (h.Room == 0) return Go(1, "down the ledges");
        if (h.Room == 1) return Go(-1, "down the ledges");
        return Safest(w, 50, "under the drips", new Controls(-1), default, new Controls(1));
    }

    /// <summary>The way through the sunken tunnels, room by room, by the air pocket.</summary>
    private static readonly (double X, double Y)[][] Swim =
    [
        [(300, 450), (540, 630), (760, 630), (1050, 500)],
        [(300, 500), (535, 470), (535, 370), (700, 500), (760, 645), (920, 645), (1050, 500)],
        [(500, 480), (700, 420), (728, 370)],
    ];

    private Controls Sunken(World w)
    {
        var h = w.Hero;
        if (h.Act != Act.Swim) return Go(1, h.Room == 0 ? "into the water" : "out of the water");
        if (_waypointRoom != h.Room) { _waypointRoom = h.Room; _waypoint = 0; }
        var route = Swim[h.Room];
        // A waypoint at the surface is reached only with a breath taken there.
        while (_waypoint < route.Length && Math.Abs(route[_waypoint].X - h.X) <= 8 && Math.Abs(route[_waypoint].Y - h.Y) <= 8
               && (route[_waypoint].Y > Sections.WaterTop + 50 || h.Breath >= Sim.BreathSeconds)) _waypoint++;
        if (_waypoint >= route.Length) return Go(1, "climbing out");
        var (x, y) = route[_waypoint];
        Doing = $"swimming to {x},{y}";
        return new Controls(Math.Abs(x - h.X) > 6 ? Math.Sign(x - h.X) : 0, Up: y < h.Y - 6, Down: y > h.Y + 6);
    }

    private Controls Plant(World w)
    {
        var h = w.Hero;
        if (h.Room == 0) return Safest(w, 36, "through the barriers", new Controls(1, Run: true), default, new Controls(-1));
        if (w.Switch <= 0)
        {
            if (h.X < 470) return Go(1, "up to the barrier");
            if (h.Facing != 1) return Go(1, "turning to the switch");
            if (w.Bolts.Any(b => b.Side == Side.Hero)) return Wait(w, "watching the shot");
            return Hold(0, "shooting the switch");
        }
        return Go(1, "through while it's off", run: true);
    }

    private Controls Bridge(World w)
    {
        var h = w.Hero;
        if (h.Room == 0 && h.Act == Act.Run && h.X >= 410 && h.X <= Sections.GapFrom) return Go(1, "leaping the gap", run: true, up: true);
        return Go(1, h.Room == 0 ? "running for the gap" : "along the bridge", run: h.Room == 0);
    }
}

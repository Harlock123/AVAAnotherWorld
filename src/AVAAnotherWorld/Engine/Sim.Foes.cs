namespace AVAAnotherWorld.Engine;

/// <summary>The foes, the friend, the bolts and shields, the cage, and the hazards of each section.</summary>
public static partial class Sim
{
    public const double BoltSpeed = 1100, HeroGun = 92, AlienGun = 116, ShieldOffset = 44, ShieldHeight = 150, HeroShieldSeconds = 9, GuardShieldSeconds = 7;
    public const double GuardWalk = 120, GuardTurn = 0.2, GuardRecoil = 0.3, WardenCharge = 1.6, WardenEvery = 4.5, WardenFirst = 2.6;
    public const double LeechSpeed = 30, LeechRoof = 120, LeechBite = 26;
    public const double BeastSpeed = 290, BeastReach = 50, BeastRise = 0.55, BeastBank = 770;
    public const double TentacleWake = 1.0, TentacleRate = 120, TentacleMax = Field.Height + TentacleDepth - (Sections.Surface + 40), TentacleDepth = 60;
    public const double DoorSeconds = 6, SwitchSeconds = 4.5, FriendWalk = 170;
    public const double DripPeriod = 1.5, DripGap = 0.5;
    public const double CageHalf = 75, CageSpring = 8, CageDamp = 0.08, CagePump = 0.55, CageBreak = 0.62, CageCrush = 80;
    public const double PatrolFrom = 200, PatrolTo = 820, PatrolSpeed = 70;

    /// <summary>How high the tentacles reach: they wake below the bottom of the screen and rise.</summary>
    public static double TentacleTip(World w) => Field.Height + TentacleDepth - w.Tentacles;

    public static bool BarrierOn(World w, Barrier b) => b.Switched ? w.Switch <= 0 : (w.Clock + b.Offset) % b.Period < b.On;

    /// <summary>Where the drop from a drip is now (it falls from the roof, splashes, and the next one forms).</summary>
    public static double DripY(World w, int i)
    {
        double t = (w.Clock + i * DripGap) % DripPeriod;
        return Sections.DripRoof + 0.5 * Gravity * t * t;
    }

    // ───────────────────────────── foes ─────────────────────────────

    private static void FoeStep(World w, Body f)
    {
        f.T += Field.Step;
        switch (f.Kind)
        {
            case Kind.Leech: LeechStep(w, f); break;
            case Kind.Beast: BeastStep(w, f); break;
            case Kind.Guard or Kind.Warden when w.Section != 3: GuardStep(w, f); break;
        }
    }

    /// <summary>A leech: hanging from the roof until the hero comes under it, then dropping; on the ground, crawling at him.</summary>
    private static void LeechStep(World w, Body f)
    {
        double dt = Field.Step;
        var h = w.Hero;
        bool here = h.Alive && h.Room == f.Room;
        if (f.Act == Act.Stand)
        {
            if (here && Math.Abs(h.X - f.To) < 30) { Begin(f, Act.Fall); f.Vy = 0; }
            return;
        }
        if (f.Act == Act.Fall)
        {
            f.Vy += Gravity * dt;
            f.Y += f.Vy * dt;
            if (here && Math.Abs(h.X - f.X) < 22 && f.Y >= h.Y - h.Height && f.Y <= h.Y + 4) { Kill(w, h, "a leech"); w.Heard.Add(Sfx.Bite); }
            if (w.RoomOf(f).Floors.FirstOrDefault(l => l.Under(f.X)) is { } floor && f.Y >= floor.YAt(f.X))
            {
                f.Y = floor.YAt(f.X);
                Begin(f, Act.Walk);
                w.Heard.Add(Sfx.Plop);
            }
            return;
        }
        if (!here || Math.Abs(h.Y - f.Y) > 30 || Math.Abs(h.X - f.X) > 420) return;
        f.Facing = Math.Sign(h.X - f.X) is var s && s != 0 ? s : f.Facing;
        f.X += f.Facing * LeechSpeed * dt;
        f.Stride += LeechSpeed * dt;
        if (Math.Abs(h.X - f.X) < LeechBite && h.OnGround) { Kill(w, h, "a leech"); w.Heard.Add(Sfx.Bite); }
    }

    /// <summary>The beast: up over the cliff edge with a roar, then after the hero, room to room, until the pool stops it.</summary>
    private static void BeastStep(World w, Body f)
    {
        double dt = Field.Step;
        var h = w.Hero;
        if (!f.Awake)
        {
            if (w.Clock < f.Wake) return;
            f.Awake = true;
            Begin(f, Act.Leap);
            w.Heard.Add(Sfx.Roar);
            return;
        }
        if (f.Act == Act.Leap)
        {
            double k = Math.Min(1, f.T / BeastRise);
            f.X = 900 - 130 * k;
            f.Y = 760 - 160 * k - Math.Sin(Math.PI * k) * 90;
            if (k >= 1) Begin(f, Act.Run);
            return;
        }
        if (f.Act == Act.Stand) return;
        int dir = h.Room == f.Room ? Math.Sign(h.X - f.X) : Math.Sign(h.Room - f.Room);
        if (dir != 0) f.Facing = dir;
        f.X += f.Facing * BeastSpeed * dt;
        f.Stride += BeastSpeed * dt;
        var room = w.RoomOf(f);
        if (room.Water != null && f.X < BeastBank)
        {
            // The pool: it can't cross; it paces the bank and roars.
            f.X = BeastBank;
            if (f.Act != Act.Turn) { Begin(f, Act.Turn); w.Heard.Add(Sfx.Roar); }
        }
        if (f.X < 0 && room.Left >= 0) { f.Room = room.Left; f.X += Field.Width; }
        else if (f.X > Field.Width && room.Right >= 0) { f.Room = room.Right; f.X -= Field.Width; }
        if (w.RoomOf(f).FloorAt(f.X, f.Y, 60) is { } floor) f.Y = floor.YAt(f.X);
        if (h.Alive && h.Room == f.Room && Math.Abs(h.X - f.X) < BeastReach && Math.Abs(h.Y - f.Y) < 90 && h.Act is not (Act.Swing or Act.Swim))
        {
            Kill(w, h, "the beast");
            Begin(f, Act.Stand);
            w.Heard.Add(Sfx.Roar);
        }
    }

    /// <summary>
    /// A guard (or the warden): wakes when the hero comes into his room (some walk in from the side), faces him, shields
    /// against his shots, and fires. The warden also charges super shots, glowing as he does.
    /// </summary>
    private static void GuardStep(World w, Body g)
    {
        double dt = Field.Step;
        var h = w.Hero;
        if (!g.Awake)
        {
            if (h.Room != g.Room || w.Clock - w.Entered < g.Wake) return;
            g.Awake = true;
            g.Cool = 0.9 + w.Dice.Next() * 0.5;
            if (g.Kind == Kind.Warden) w.LastSuper = w.Clock - (WardenEvery - WardenFirst);
            w.Heard.Add(Sfx.Shout);
        }
        if (!double.IsNaN(g.To))
        {
            // Walking in.
            g.Facing = Math.Sign(g.To - g.X);
            double was = g.X;
            g.X = Move(w, g, g.X + g.Facing * Math.Min(GuardWalk * dt, Math.Abs(g.To - g.X)));
            g.Stride += Math.Abs(g.X - was);
            if (g.Act != Act.Walk) Begin(g, Act.Walk);
            if (Math.Abs(g.X - g.To) < 1 || Math.Abs(g.X - was) < 1e-6) { g.To = double.NaN; Begin(g, Act.Aim); g.Cool = Math.Max(g.Cool, 0.6); }
            return;
        }
        if (!h.Alive || h.Room != g.Room) { if (g.Act is Act.Charge) Begin(g, Act.Aim); return; }
        switch (g.Act)
        {
            case Act.Turn when g.T < GuardTurn:
            case Act.Recoil when g.T < GuardRecoil:
            case Act.Cast when g.T < CastSeconds:
                return;
            case Act.Charge when g.T < WardenCharge:
                return;
            case Act.Charge:
                Fire(w, g, true);
                Begin(g, Act.Recoil);
                return;
            case Act.Turn or Act.Recoil or Act.Cast or Act.Stand or Act.Walk:
                Begin(g, Act.Aim);
                break;
        }
        int toward = Math.Sign(h.X - g.X);
        if (toward != 0 && toward != g.Facing) { g.Facing = toward; Begin(g, Act.Turn); return; }
        bool covered = w.Shields.Any(s => s.Side == g.Side && s.Room == g.Room && (s.X - g.X) * g.Facing > 0 && (h.X - s.X) * g.Facing > 0);
        // A shot coming: a shield up, if he's quick enough.
        foreach (var b in w.Bolts)
        {
            if (b.Gone || b.Seen || b.Side == g.Side || b.Room != g.Room || Math.Sign(g.X - b.X) != b.Dir || Math.Abs(g.X - b.X) > 420) continue;
            b.Seen = true;
            if (!covered && w.Dice.Next() < g.Skill) { Raise(w, g); Begin(g, Act.Cast); return; }
        }
        // The hero charging up, or now and then: a shield anyway.
        if (!covered && w.Dice.Next() < (h.Act == Act.Charge ? g.Skill : 0.08) * dt) { Raise(w, g); Begin(g, Act.Cast); return; }
        if ((g.Cool -= dt) > 0) return;
        g.Cool = g.Gap * (0.75 + 0.5 * w.Dice.Next());
        if (g.Kind == Kind.Warden && w.Clock - w.LastSuper >= WardenEvery)
        {
            w.LastSuper = w.Clock;
            Begin(g, Act.Charge);
            w.Heard.Add(Sfx.Charge);
            return;
        }
        Fire(w, g, false);
        Begin(g, Act.Recoil);
    }

    /// <summary>The friend: follows the hero, and at a door goes to its panel and works it open.</summary>
    private static void FriendStep(World w, Body f)
    {
        double dt = Field.Step;
        var h = w.Hero;
        f.T += dt;
        if (w.DoorOpen) w.Door += dt;
        if (f.Room != h.Room) { f.Room = h.Room; f.X = h.X < Field.Width / 2 ? -40 : Field.Width + 40; }
        var room = w.RoomOf(f);
        bool door = room.Door != null && !w.DoorOpen;
        double target = door ? Sections.PanelX : h.X - 90 * h.Facing;
        if (Math.Abs(f.X - target) > (door ? 3 : 30))
        {
            f.Facing = Math.Sign(target - f.X);
            double step = Math.Min(FriendWalk * dt, Math.Abs(target - f.X));
            f.X += f.Facing * step;
            f.Stride += step;
            if (f.Act != Act.Walk) Begin(f, Act.Walk);
        }
        else if (door)
        {
            if (f.Act != Act.Work) Begin(f, Act.Work);
            f.Facing = 1;
            w.Door += dt;
            if (w.Door - dt < DoorSeconds - 0.4 && w.Door >= DoorSeconds - 0.4) w.Heard.Add(Sfx.Work);
            if (w.DoorOpen) { w.Heard.Add(Sfx.Door); w.Sparks.Add(new Spark(f.Room, Sections.DoorX, Sections.CellFloor - 120, w.Clock, SparkKind.Sparks)); }
        }
        else
        {
            if (f.Act != Act.Stand) Begin(f, Act.Stand);
            if (Math.Abs(h.X - f.X) > 4) f.Facing = Math.Sign(h.X - f.X);
        }
        if (room.FloorAt(Math.Clamp(f.X, 0, Field.Width), f.Y, 40) is { } floor) f.Y = floor.YAt(Math.Clamp(f.X, 0, Field.Width));
    }

    // ───────────────────────────── shots and shields ─────────────────────────────

    /// <summary>A bolt from the pistol's muzzle, at the shooter's shoulder height (a crouch ducks a guard's).</summary>
    public static void Fire(World w, Body b, bool super)
    {
        w.Bolts.Add(new Bolt { Side = b.Side, Room = b.Room, X = b.X + b.Facing * 44, Y = b.Y - (b.Alien ? AlienGun : HeroGun), Dir = b.Facing, Super = super });
        w.Heard.Add(super ? Sfx.Super : Sfx.Shot);
    }

    /// <summary>A shield wall just in front: no more than two of a side's in a room (the oldest goes).</summary>
    public static void Raise(World w, Body b)
    {
        var mine = w.Shields.Where(s => s.Side == b.Side && s.Room == b.Room).ToList();
        if (mine.Count >= 2) w.Shields.Remove(mine.MaxBy(s => s.Age)!);
        w.Shields.Add(new Shield
        {
            Side = b.Side, Room = b.Room, X = b.X + b.Facing * ShieldOffset, Y = b.Y, Life = b.Kind == Kind.Hero ? HeroShieldSeconds : GuardShieldSeconds,
        });
        w.Heard.Add(Sfx.Shield);
    }

    /// <summary>
    /// The bolts fly: the first thing along the way stops each one. The other side's shield stops a shot (a super shot
    /// breaks it and flies on); a body of the other side is killed; a wall, a door or the edge of the screen ends it; a
    /// switch panel is thrown.
    /// </summary>
    private static void BoltStep(World w)
    {
        double dt = Field.Step;
        foreach (var b in w.Bolts)
        {
            if (b.Gone) continue;
            b.Age += dt;
            var room = w.Def.Rooms[b.Room];
            double x0 = b.X, x1 = b.X + b.Dir * BoltSpeed * dt;
            bool Between(double x) => (x - x0) * b.Dir >= 0 && (x1 - x) * b.Dir >= 0;
            var hits = new List<(double At, object What)>();
            foreach (var s in w.Shields)
                if (s.Side != b.Side && s.Room == b.Room && b.Y >= s.Y - ShieldHeight && b.Y <= s.Y && Between(s.X)) hits.Add((s.X, s));
            foreach (var t in Targets(w, b.Side))
                if (t.Alive && t.Room == b.Room && b.Y >= t.Y - t.Height && b.Y <= t.Y + 2 && Between(t.X - b.Dir * 10)) hits.Add((t.X - b.Dir * 10, t));
            foreach (var wall in room.Walls)
                if (b.Y >= wall.Top && b.Y <= wall.Bottom && Between(wall.X)) hits.Add((wall.X, wall));
            if (room.Door is { } door && !w.DoorOpen && Between(door)) hits.Add((door, "door"));
            if (room.Panel is { } panel && b.Side == Side.Hero && b.Y >= panel.Y0 && b.Y <= panel.Y1 && Between(panel.X0)) hits.Add((panel.X0, panel));
            foreach (var (at, what) in hits.OrderBy(e => (e.At - x0) * b.Dir))
            {
                switch (what)
                {
                    case Shield s when b.Super:
                        w.Shields.Remove(s);
                        w.Heard.Add(Sfx.Break);
                        w.Sparks.Add(new Spark(b.Room, s.X, b.Y, w.Clock, SparkKind.Burst));
                        continue;
                    case Shield s:
                        s.Struck = w.Clock;
                        w.Heard.Add(Sfx.Block);
                        w.Sparks.Add(new Spark(b.Room, s.X, b.Y, w.Clock, SparkKind.Block));
                        break;
                    case Body t:
                        Kill(w, t, t.Kind == Kind.Hero ? (b.Super ? "a super shot" : "a guard's shot") : "a shot");
                        w.Heard.Add(Sfx.Zap);
                        w.Sparks.Add(new Spark(b.Room, t.X, b.Y, w.Clock, SparkKind.Hit));
                        break;
                    case Zone:
                        w.Switch = SwitchSeconds;
                        w.Heard.Add(Sfx.Switch);
                        w.Sparks.Add(new Spark(b.Room, at, b.Y, w.Clock, SparkKind.Sparks));
                        break;
                    default:
                        w.Sparks.Add(new Spark(b.Room, at, b.Y, w.Clock, SparkKind.Sparks));
                        break;
                }
                b.Gone = true;
                b.X = at;
                break;
            }
            if (b.Gone) continue;
            b.X = x1;
            if (b.X < -40 || b.X > Field.Width + 40) b.Gone = true;
        }
        w.Bolts.RemoveAll(b => b.Gone);
    }

    private static IEnumerable<Body> Targets(World w, Side shooter) =>
        shooter == Side.Hero ? w.Foes.Where(f => f.Kind is Kind.Guard or Kind.Warden or Kind.Beast) : [w.Hero];

    // ───────────────────────────── the cage ─────────────────────────────

    /// <summary>
    /// Section three: hanging in a cage over the hall. Left and right rock it; pushed with the swing it swings higher,
    /// against it lower. Swung too far, the chain snaps and it falls; if the guard is under it then, he's crushed. If not,
    /// he turns and shoots.
    /// </summary>
    private static void CageStep(World w, Controls c)
    {
        double dt = Field.Step;
        var cg = w.Cage;
        var h = w.Hero;
        var g = w.Foes[0];
        if (!cg.Broken)
        {
            double pump = c.Dx * (c.Dx * cg.Omega >= 0 ? CagePump : CagePump * 0.5);
            double was = cg.Omega;
            cg.Omega += (-CageSpring * Math.Sin(cg.Theta) - CageDamp * cg.Omega + pump) * dt;
            cg.Theta += cg.Omega * dt;
            cg.Peak = Math.Max(cg.Peak * (1 - 0.02 * dt), Math.Abs(cg.Theta));
            if (Math.Sign(was) != Math.Sign(cg.Omega) && Math.Abs(cg.Theta) > 0.12) w.Heard.Add(Sfx.Creak);
            cg.X = Sections.CageX + Sections.Chain * Math.Sin(cg.Theta);
            cg.Y = Sections.CageTop + Sections.Chain * Math.Cos(cg.Theta);
            if (Math.Abs(cg.Theta) >= CageBreak)
            {
                cg.Broken = true;
                cg.Vx = Sections.Chain * cg.Omega * Math.Cos(cg.Theta);
                cg.Vy = -Sections.Chain * cg.Omega * Math.Sin(cg.Theta);
                w.Heard.Add(Sfx.Snap);
            }
        }
        else if (!cg.Landed)
        {
            cg.Vy += Gravity * dt;
            cg.X += cg.Vx * dt;
            cg.Y += cg.Vy * dt;
            if (cg.Y + CageHalf >= Sections.HallFloor)
            {
                cg.Y = Sections.HallFloor - CageHalf;
                cg.Landed = true;
                cg.Since = w.Clock;
                w.Heard.Add(Sfx.Crash);
                w.Sparks.Add(new Spark(0, cg.X, Sections.HallFloor, w.Clock, SparkKind.Dust));
                if (g.Alive && Math.Abs(g.X - cg.X) < CageCrush) { Kill(w, g, "the cage"); cg.Crushed = true; }
            }
        }
        h.X = cg.X - 22;
        h.Y = cg.Landed ? Sections.HallFloor : cg.Y + CageHalf - 4;
        if (!g.Alive) return;
        if (!cg.Landed)
        {
            // Pacing the hall below.
            g.X += g.Facing * PatrolSpeed * dt;
            g.Stride += PatrolSpeed * dt;
            if (g.X > PatrolTo) { g.X = PatrolTo; g.Facing = -1; }
            if (g.X < PatrolFrom) { g.X = PatrolFrom; g.Facing = 1; }
            return;
        }
        // Missed: he turns, takes aim, and fires.
        if (g.Act == Act.Walk) { Begin(g, Act.Aim); g.Facing = Math.Sign(h.X - g.X) is var s && s != 0 ? s : g.Facing; }
        if (g.Act == Act.Aim && w.Clock - cg.Since > 0.8 && h.Alive) { Fire(w, g, false); Begin(g, Act.Recoil); }
    }

    // ───────────────────────────── hazards ─────────────────────────────

    /// <summary>What kills where the hero is: the tentacles in the pool, a barrier that's on, a drop of acid.</summary>
    private static void Hazards(World w, Body h)
    {
        var room = w.Here;
        if (room.Tentacles && h.Act == Act.Swim && TentacleTip(w) < h.Y + 40)
        {
            Kill(w, h, "the tentacles");
            w.Heard.Add(Sfx.Tentacle);
            return;
        }
        foreach (var b in room.Barriers)
            if (BarrierOn(w, b) && Math.Abs(h.X - b.X) < 22) { Kill(w, h, "the barrier"); w.Heard.Add(Sfx.Buzz); return; }
        for (int i = 0; i < room.Drips.Count; i++)
        {
            double y = DripY(w, i), x = room.Drips[i], floor = room.FloorAt(x, Sections.GrottoFloor, 40)?.YAt(x) ?? Field.Height;
            if (y < floor && Math.Abs(h.X - x) < 16 && y >= h.Y - h.Height && y <= h.Y) { Kill(w, h, "acid"); w.Heard.Add(Sfx.Fizz); return; }
            double before = (w.Clock - Field.Step + i * DripGap) % DripPeriod;
            if (Sections.DripRoof + 0.5 * Gravity * before * before < floor && y >= floor) w.Heard.Add(Sfx.Drip);
        }
    }
}

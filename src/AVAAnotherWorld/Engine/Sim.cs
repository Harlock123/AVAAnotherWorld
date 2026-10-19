namespace AVAAnotherWorld.Engine;

/// <summary>
/// The rules, one 60th of a second at a time. The hero walks, runs, leaps, falls, swims and fights in one room at a time;
/// the foes, the bolts and shields, the cage, the tentacles, barriers and drips go on around him. One touch of anything
/// deadly is the end of him.
/// </summary>
public static partial class Sim
{
    public const double Gravity = 1500, MaxFall = 300;
    public const double WalkSpeed = 150, RunSpeed = 320, SwimSpeed = 190, SinkSpeed = 30;
    public const double RunLeapVx = 380, RunLeapVy = -480, WalkLeapVx = 220, WalkLeapVy = -400, HopVy = -380;
    public const double TurnSeconds = 0.16, KickSeconds = 0.45, KickFrom = 0.12, KickTo = 0.3, KickReach = 84;
    public const double ClimbSeconds = 0.6, SwingSeconds = 1.0, BreathSeconds = 12;
    /// <summary>Fire held this long makes a shield on letting go; this long, a super shot.</summary>
    public const double ShieldHold = 0.3, SuperHold = 1.2;
    public const double RecoilSeconds = 0.22, CastSeconds = 0.3;
    public const double Radius = 16, StepLength = 64;

    public static void Start(World w, SectionDef def)
    {
        w.Def = def;
        w.Clock = 0;
        w.Entered = 0;
        w.Done = false;
        w.Met = -1;
        w.Cause = "";
        w.Door = 0;
        w.Switch = 0;
        w.LastSuper = -10;
        w.Bolts = [];
        w.Shields = [];
        w.Sparks = [];
        w.Heard = [];
        w.Dice = new Dice(def.Number * 31 + 7);
        w.Hero = new Body
        {
            Kind = Kind.Hero, Room = def.StartRoom, X = def.StartX, Y = def.StartY, Facing = def.Facing, Act = def.StartAct, Gun = def.Gun, Top = def.StartY,
        };
        w.Foes = [];
        foreach (var f in def.Foes)
        {
            var b = new Body
            {
                Kind = f.Kind, Room = f.Room, X = f.X, Y = f.Y, Facing = f.Facing, Skill = f.Skill, Gap = f.Gap, Wake = f.Wake, To = f.To, Top = f.Y,
                Act = f.Kind == Kind.Leech && double.IsNaN(f.To) ? Act.Walk : f.Kind is Kind.Guard or Kind.Warden ? Act.Aim : Act.Stand,
            };
            if (f.Kind is Kind.Guard && def.Number == 3) b.Act = Act.Walk;
            w.Foes.Add(b);
            if (f.Shielded) w.Shields.Add(new Shield { Side = Side.Alien, Room = f.Room, X = f.X + f.Facing * ShieldOffset, Y = f.Y, Life = double.PositiveInfinity });
        }
        w.Friend = def.Friend is { } fr ? new Body { Kind = Kind.Friend, Room = fr.Room, X = fr.X, Y = def.StartY, Facing = 1 } : null;
        w.Tentacles = def.Number == 2 ? TentacleMax : 0;
        w.Cage = new Cage();
    }

    public static void Step(World w, Controls c)
    {
        double dt = Field.Step;
        w.Clock += dt;
        if (w.Sparks.Count > 0) w.Sparks.RemoveAll(s => w.Clock - s.Born > 1.2);
        if (w.Done) return;
        var h = w.Hero;
        if (w.Section == 3) CageStep(w, c);
        else
        {
            if (w.Clock > TentacleWake) w.Tentacles = Math.Min(TentacleMax, w.Tentacles + TentacleRate * dt);
            if (w.Switch > 0 && (w.Switch -= dt) <= 0) w.Heard.Add(Sfx.Buzz);
            if (h.Alive) HeroStep(w, h, c);
            if (w.Friend != null) FriendStep(w, w.Friend);
        }
        foreach (var f in w.Foes)
            if (f.Alive) FoeStep(w, f);
        BoltStep(w);
        for (int i = w.Shields.Count - 1; i >= 0; i--)
        {
            var s = w.Shields[i];
            s.Age += dt;
            if ((s.Life -= dt) <= 0) w.Shields.RemoveAt(i);
        }
        if (h.Alive) Hazards(w, h);
        if (h.Alive && w.Met < 0 && Goal(w)) w.Met = 0;
        if (w.Met >= 0 && h.Alive && (w.Met += dt) >= MetSeconds(w.Section)) w.Done = true;
    }

    /// <summary>Whether the section's goal is met: out of the room it ends in, the cage down on the guard, the warden dead.</summary>
    public static bool Goal(World w)
    {
        var h = w.Hero;
        return w.Section switch
        {
            1 => h.Room == 3 && h.X > 640 && h.OnGround,
            2 => h.Room == 0,
            3 => w.Cage.Crushed,
            4 => h.Room == 1 && h.X > 975,
            5 => h.Room == 2 && h.X <= 30,
            6 => h.Room == 2 && h.X > 990 && h.OnGround,
            7 => h.Room == 1 && h.X > 975,
            8 => h.Room == 1 && h.X > 990,
            9 => w.Foes.All(f => !f.Alive),
            _ => false,
        };
    }

    /// <summary>How long after the goal is met the section is over (long enough to see the cage land or the warden fall).</summary>
    public static double MetSeconds(int section) => section switch { 3 => 1.6, 9 => 1.8, _ => 0.15 };

    public static void Begin(Body b, Act a, double dur = 0)
    {
        b.Act = a;
        b.T = 0;
        b.Dur = dur;
        b.Struck = false;
    }

    public static void Kill(World w, Body b, string cause)
    {
        if (!b.Alive) return;
        b.Act = Act.Dead;
        b.T = 0;
        b.Cause = cause;
        b.Holding = false;
        if (b.Kind == Kind.Hero)
        {
            w.Cause = cause;
            w.Heard.Add(Sfx.Die);
        }
        else w.Heard.Add(b.Kind switch { Kind.Leech => Sfx.Squish, Kind.Beast => Sfx.Roar, _ => Sfx.Cry });
    }

    // ───────────────────────────── the hero ─────────────────────────────

    private static void HeroStep(World w, Body h, Controls c)
    {
        double dt = Field.Step;
        h.T += dt;
        switch (h.Act)
        {
            case Act.Swim: Swim(w, h, c); return;
            case Act.Climb:
                if (h.T >= ClimbSeconds) { h.X = h.Tx; h.Y = h.Ty; h.Breath = BreathSeconds; Begin(h, Act.Stand); }
                return;
            case Act.Swing: Swing(w, h); return;
            case Act.Leap or Act.Hop or Act.Fall: Air(w, h); return;
        }
        var room = w.RoomOf(h);
        if (room.FloorAt(h.X, h.Y) is not { } floor) { Fall(h, 0); return; }
        h.Y = floor.YAt(h.X);
        switch (h.Act)
        {
            case Act.Turn when h.T < TurnSeconds: return;
            case Act.Land when h.T < h.Dur: return;
            case Act.Kick:
                if (!h.Struck && h.T >= KickFrom && h.T <= KickTo) KickHit(w, h);
                if (h.T < KickSeconds) return;
                Begin(h, Act.Stand);
                break;
            case Act.Recoil when h.T >= RecoilSeconds: Begin(h, Act.Aim); break;
            case Act.Cast when h.T >= CastSeconds: Begin(h, Act.Aim); break;
            case Act.Turn or Act.Land: Begin(h, Act.Stand); break;
        }
        if (h.Gun ? Trigger(w, h, c) : Kick(w, h, c)) return;
        if (c.Up)
        {
            bool moving = c.Dx == h.Facing;
            if (!moving) { Leap(w, h, Act.Hop, 0, HopVy); return; }
            bool running = c.Run && h.Act == Act.Run;
            Leap(w, h, Act.Leap, (running ? RunLeapVx : WalkLeapVx) * h.Facing, running ? RunLeapVy : WalkLeapVy);
            return;
        }
        if (c.Down)
        {
            if (h.Act != Act.Crouch) Begin(h, Act.Crouch);
            return;
        }
        if (c.Dx != 0 && c.Dx != h.Facing)
        {
            h.Facing = c.Dx;
            Begin(h, Act.Turn);
            return;
        }
        if (c.Dx == h.Facing)
        {
            var act = c.Run ? Act.Run : Act.Walk;
            if (h.Act != act) Begin(h, act);
            Walk(w, h, c.Run ? RunSpeed : WalkSpeed);
            return;
        }
        if (h.Act is Act.Walk or Act.Run or Act.Crouch) Begin(h, Act.Stand);
    }

    /// <summary>Along the floor: walls, the other side's bodies and shields in the way; off the end of a ledge, a fall.</summary>
    private static void Walk(World w, Body b, double speed)
    {
        double dt = Field.Step;
        double was = b.X;
        b.X = Move(w, b, b.X + b.Facing * speed * dt);
        double moved = Math.Abs(b.X - was);
        if (b.Kind == Kind.Hero && Math.Floor((b.Stride + moved) / StepLength) > Math.Floor(b.Stride / StepLength)) w.Heard.Add(Sfx.Step);
        b.Stride += moved;
        Cross(w, b);
        var room = w.RoomOf(b);
        if (room.FloorAt(b.X, b.Y) is { } floor) b.Y = floor.YAt(b.X);
        else Fall(b, b.Facing * speed * 0.6);
    }

    private static void Fall(Body b, double vx)
    {
        Begin(b, Act.Fall);
        b.Vx = vx;
        b.Vy = 0;
        b.Top = b.Y;
    }

    private static void Leap(World w, Body h, Act a, double vx, double vy)
    {
        Begin(h, a);
        h.Vx = vx;
        h.Vy = vy;
        h.Top = h.Y;
        w.Heard.Add(Sfx.Jump);
    }

    /// <summary>Through the air: gravity, walls, a vine to catch, water to fall in, a floor to land on, or the room below.</summary>
    private static void Air(World w, Body h)
    {
        double dt = Field.Step;
        var room = w.RoomOf(h);
        double prevY = h.Y;
        h.Vy += Gravity * dt;
        double to = h.X + h.Vx * dt, x = Move(w, h, to);
        if (Math.Abs(x - to) > 0.01) h.Vx = 0;
        h.X = x;
        h.Y += h.Vy * dt;
        h.Top = Math.Min(h.Top, h.Y);
        if (h.Act == Act.Leap && room.Vine is { } vine && Math.Abs(h.X - vine.X) < 50 && h.Y - h.Height <= vine.Tip + 24 && h.Y - h.Height > 40)
        {
            Begin(h, Act.Swing);
            h.Tx = Math.Asin(Math.Clamp((h.X - vine.X) / vine.Tip, -1, 1));
            w.Heard.Add(Sfx.Grab);
            return;
        }
        if (room.Water is { } water && h.Vy > 0 && water.Contains(h.X, h.Y - 30))
        {
            Begin(h, Act.Swim);
            h.Y -= 50;
            h.Vx = h.Vy = 0;
            w.Heard.Add(Sfx.Splash);
            w.Sparks.Add(new Spark(h.Room, h.X, water.Y0, w.Clock, SparkKind.Splash));
            return;
        }
        if (h.Vy > 0)
            foreach (var l in room.Floors)
            {
                if (!l.Under(h.X)) continue;
                double ly = l.YAt(h.X);
                if (prevY <= ly + 0.5 && h.Y >= ly)
                {
                    h.Y = ly;
                    double fall = ly - h.Top;
                    h.Vx = h.Vy = 0;
                    if (fall > MaxFall) { Kill(w, h, "a fall"); return; }
                    w.Heard.Add(Sfx.Land);
                    Begin(h, Act.Land, fall > 150 ? 0.3 : 0.1);
                    return;
                }
            }
        if (h.Y > Field.Height)
        {
            if (room.Down >= 0) { h.Room = room.Down; h.Y -= Field.Height; h.Top -= Field.Height; w.Entered = w.Clock; }
            else { Kill(w, h, "a fall"); return; }
        }
        Cross(w, h);
    }

    /// <summary>On the vine: an arc over the water, then let go into a leap the way he's facing.</summary>
    private static void Swing(World w, Body h)
    {
        var vine = w.RoomOf(h).Vine!.Value;
        double k = Math.Min(1, h.T / SwingSeconds), s = k * k * (3 - 2 * k);
        double phi = h.Tx + (0.8 * h.Facing - h.Tx) * s;
        h.X = vine.X + vine.Tip * Math.Sin(phi);
        h.Y = vine.Tip * Math.Cos(phi) + 124;
        if (h.T < SwingSeconds) return;
        Begin(h, Act.Leap);
        h.Vx = 240 * h.Facing;
        h.Vy = -120;
        h.Top = h.Y;
    }

    /// <summary>In the water: swim any way (up is up), out of breath under the rock, out onto a bank at the surface.</summary>
    private static void Swim(World w, Body h, Controls c)
    {
        double dt = Field.Step;
        var room = w.RoomOf(h);
        var water = room.Water!.Value;
        if (c.Dx != 0) h.Facing = c.Dx;
        double vx = c.Dx * SwimSpeed, vy = c.Up ? -SwimSpeed : c.Down ? SwimSpeed : SinkSpeed;
        double lo = room.Left >= 0 && water.X0 <= 0 ? -40 : water.X0 + 22, hi = room.Right >= 0 && water.X1 >= Field.Width ? Field.Width + 40 : water.X1 - 22;
        double nx = Math.Clamp(h.X + vx * dt, lo, hi);
        if (!Solid(room, nx, h.Y)) h.X = nx;
        double ny = Math.Clamp(h.Y + vy * dt, water.Y0 + 44, water.Y1 - 30);
        if (!Solid(room, h.X, ny)) h.Y = ny;
        h.Stride += Math.Sqrt(vx * vx + vy * vy) * dt;
        h.Vx = vx;
        h.Vy = vy;
        double tilt = vx == 0 ? (vy < 0 ? 0.2 : vy > SinkSpeed ? 2.4 : 0.35) : vy < 0 ? 0.85 : vy > SinkSpeed ? 2.0 : 1.45;
        h.Tilt += (tilt - h.Tilt) * Math.Min(1, dt * 7);
        bool under = h.Y - 44 > water.Y0 + 1 || room.Rocks.Any(r => r.Contains(h.X, water.Y0 + 5));
        if (under) h.Breath -= dt;
        else
        {
            if (h.Breath < BreathSeconds - 3) w.Heard.Add(Sfx.Gasp);
            h.Breath = BreathSeconds;
        }
        if (h.Breath <= 0) { Kill(w, h, "drowning"); return; }
        if (!under && c.Dx != 0)
            foreach (var l in room.Floors)
            {
                double edge = c.Dx > 0 ? l.X0 : l.X1;
                if (Math.Abs(l.YAt(edge) - water.Y0) < 60 && Math.Abs(h.X - edge) < 56)
                {
                    Begin(h, Act.Climb);
                    h.Tx = edge + c.Dx * 30;
                    h.Ty = l.YAt(h.Tx);
                    w.Heard.Add(Sfx.Climb);
                    return;
                }
            }
        if (h.X < 0 && room.Left >= 0) { h.Room = room.Left; h.X += Field.Width; w.Entered = w.Clock; }
        else if (h.X > Field.Width && room.Right >= 0) { h.Room = room.Right; h.X -= Field.Width; w.Entered = w.Clock; }
    }

    public static bool Solid(Room room, double x, double y) => room.Rocks.Any(r => r.Touches(x, y, 24));

    /// <summary>A kick: a leech in reach in front is squashed.</summary>
    private static bool Kick(World w, Body h, Controls c)
    {
        bool pressed = c.Fire && !h.Holding;
        h.Holding = c.Fire;
        if (!pressed) return false;
        Begin(h, Act.Kick);
        w.Heard.Add(Sfx.Kick);
        return true;
    }

    private static void KickHit(World w, Body h)
    {
        foreach (var f in w.Foes)
        {
            if (f.Kind != Kind.Leech || !f.Alive || f.Room != h.Room || f.Act == Act.Fall) continue;
            double d = (f.X - h.X) * h.Facing;
            if (d >= 0 && d <= KickReach && Math.Abs(f.Y - h.Y) < 30)
            {
                h.Struck = true;
                Kill(w, f, "a kick");
                w.Sparks.Add(new Spark(f.Room, f.X, f.Y - 8, w.Clock, SparkKind.Squish));
            }
        }
    }

    /// <summary>
    /// The pistol. Fire goes down: the pistol comes up. Let go quickly: a shot. Hold a moment: a shield wall in front.
    /// Hold longer (it glows): a super shot that breaks shields.
    /// </summary>
    public static bool Trigger(World w, Body b, Controls c)
    {
        if (c.Fire)
        {
            if (!b.Holding)
            {
                b.Holding = true;
                b.Hold = 0;
                if (b.Act is not (Act.Aim or Act.Charge)) Begin(b, Act.Aim);
                return true;
            }
            b.Hold += Field.Step;
            if (b.Act == Act.Aim && b.Hold >= ShieldHold) { Begin(b, Act.Charge); w.Heard.Add(Sfx.Charge); }
            return true;
        }
        if (!b.Holding) return false;
        b.Holding = false;
        double hold = b.Hold;
        b.Hold = 0;
        if (b.Act is not (Act.Aim or Act.Charge)) return false;
        if (hold < ShieldHold) { Fire(w, b, false); Begin(b, Act.Recoil); }
        else if (hold < SuperHold) { Raise(w, b); Begin(b, Act.Cast); }
        else { Fire(w, b, true); Begin(b, Act.Recoil); }
        return true;
    }

    /// <summary>Moves a body along to <paramref name="to"/> as far as nothing stops it: walls, a shut door, the other side's shields and bodies.</summary>
    public static double Move(World w, Body b, double to)
    {
        double from = b.X;
        int dir = Math.Sign(to - from);
        if (dir == 0) return from;
        var room = w.RoomOf(b);
        double top = b.Y - b.Height, bottom = b.Y - 2, limit = to;
        void Line(double x, double r)
        {
            if (dir > 0 && from + r <= x + 0.5 && to + r > x) limit = Math.Min(limit, x - r);
            if (dir < 0 && from - r >= x - 0.5 && to - r < x) limit = Math.Max(limit, x + r);
        }
        foreach (var wall in room.Walls)
            if (wall.Top < bottom && wall.Bottom > top) Line(wall.X, Radius);
        if (room.Door is { } door && !w.DoorOpen) Line(door, Radius);
        if (b.Kind is Kind.Hero or Kind.Guard or Kind.Warden)
        {
            foreach (var s in w.Shields)
                if (s.Side != b.Side && s.Room == b.Room && Math.Abs(s.Y - b.Y) < 40) Line(s.X, Radius);
            foreach (var o in Others(w, b))
                if (o.Alive && o.Room == b.Room && Math.Abs(o.Y - b.Y) < 40) Line(o.X, Radius * 2.4);
        }
        return limit;
    }

    private static IEnumerable<Body> Others(World w, Body b) =>
        b.Kind == Kind.Hero ? w.Foes.Where(f => f.Kind is Kind.Guard or Kind.Warden) : [w.Hero];

    /// <summary>Off the side of the screen: into the next room, or stopped at the edge of the world.</summary>
    private static void Cross(World w, Body b)
    {
        var room = w.RoomOf(b);
        if (b.X < 0)
        {
            if (room.Left >= 0) { b.Room = room.Left; b.X += Field.Width; if (b.Kind == Kind.Hero) w.Entered = w.Clock; }
            else b.X = 12;
        }
        else if (b.X > Field.Width)
        {
            if (room.Right >= 0) { b.Room = room.Right; b.X -= Field.Width; if (b.Kind == Kind.Hero) w.Entered = w.Clock; }
            else b.X = Field.Width - 12;
        }
    }
}

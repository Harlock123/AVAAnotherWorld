using Avalonia;
using Avalonia.Media;
using AVAAnotherWorld.Engine;

namespace AVAAnotherWorld.Views;

/// <summary>
/// The people: skeletons whose bones are drawn as flat polygons (the far arm and leg darker), posed by keyframes that
/// are blended smoothly from one to the next, for the rotoscoped look. The beast, the leeches and the pool's
/// tentacles are polygons too.
/// </summary>
public sealed partial class GameCanvas
{
    /// <summary>
    /// A pose, in radians: the lean of the body and the tilt of the head; each arm from hanging straight down (+ is
    /// forward) and its elbow (+ bends it forward); each leg from straight down and its knee (− bends the shin back).
    /// Grounded poses stand on their lower foot; free ones (in the air, in the water) hang their hips at Lift.
    /// </summary>
    private readonly record struct Pose(double Lean, double Head, double ArmN, double ElbowN, double ArmF, double ElbowF,
        double HipN, double KneeN, double HipF, double KneeF, double Lift = 0, bool Free = false)
    {
        public static Pose Lerp(Pose a, Pose b, double t)
        {
            static double L(double x, double y, double k) => x + (y - x) * k;
            return new Pose(L(a.Lean, b.Lean, t), L(a.Head, b.Head, t), L(a.ArmN, b.ArmN, t), L(a.ElbowN, b.ElbowN, t), L(a.ArmF, b.ArmF, t),
                L(a.ElbowF, b.ElbowF, t), L(a.HipN, b.HipN, t), L(a.KneeN, b.KneeN, t), L(a.HipF, b.HipF, t), L(a.KneeF, b.KneeF, t),
                L(a.Lift, b.Lift, t), t < 0.5 ? a.Free : b.Free);
        }

        /// <summary>The same pose with the near and far limbs swapped: the other half of a stride.</summary>
        public Pose Mirror() => this with { ArmN = ArmF, ElbowN = ElbowF, ArmF = ArmN, ElbowF = ElbowN, HipN = HipF, KneeN = KneeF, HipF = HipN, KneeF = KneeN };
    }

    /// <summary>A body's proportions and colours.</summary>
    private sealed record Build(double Thigh, double Shin, double Torso, double Neck, double Head, double Upper, double Fore,
        double LegW, double ArmW, double HipW, double ChestW,
        uint Skin, uint SkinDark, uint Top, uint TopDark, uint Legs, uint LegsDark, uint Feet, uint Hair, uint Trim, bool Alien, bool Armour);

    private static readonly Build HeroBuild = new(31, 31, 40, 6, 11.5, 24, 23, 12, 8.5, 18, 22,
        0xFFE8B490, 0xFFAF7D5E, 0xFFDDE1E3, 0xFF8C96A6, 0xFF2E3C62, 0xFF1A2240, 0xFF141418, 0xFF2E1A12, 0xFF6A4630, false, false);
    private static readonly Build GuardBuild = new(38, 39, 50, 7, 13, 30, 29, 13, 9, 20, 30,
        0xFF9C96B6, 0xFF5E587C, 0xFF2E2842, 0xFF1A1628, 0xFF463C5A, 0xFF28223A, 0xFF120E18, 0, 0xFFA83838, true, true);
    private static readonly Build WardenBuild = new(41, 41, 55, 7, 14, 32, 31, 14, 10, 22, 34,
        0xFFA49EBE, 0xFF625C82, 0xFF3C1418, 0xFF220A0E, 0xFF2A2030, 0xFF181220, 0xFF0E0A10, 0, 0xFFD0A848, true, true);
    private static readonly Build OruBuild = new(40, 40, 52, 7, 13.5, 31, 30, 14, 10, 22, 30,
        0xFFA6B092, 0xFF6C7A5E, 0xFFA6B092, 0xFF6C7A5E, 0xFF7C5836, 0xFF4E3620, 0xFF5A644C, 0, 0xFF5A3A20, true, false);

    private static Build BuildOf(Kind k) => k switch { Kind.Guard => GuardBuild, Kind.Warden => WardenBuild, Kind.Friend => OruBuild, _ => HeroBuild };

    // ───────────────────────────── the keyframes ─────────────────────────────

    private static readonly Pose Standing = new(0.02, 0, 0.06, 0.14, -0.06, 0.18, 0.05, -0.05, -0.05, -0.05);
    private static readonly Pose Crouching = new(0.42, 0.15, 0.55, 0.55, 0.3, 0.7, 1.4, -2.4, 1.15, -2.3);
    private static readonly Pose Kneeling = new(0.15, 0.25, 0.3, 0.4, 0.2, 0.5, 1.5, -1.6, -0.2, -1.55);
    private static readonly Pose Lying = new(0.0, 0.3, 2.2, 0.5, 1.2, 0.6, 0.15, -0.35, -0.05, -0.2, 0, true);
    private static readonly Pose Aiming = new(-0.03, 0.02, 1.57, 0.0, 0.25, 0.55, 0.26, -0.08, -0.2, -0.06);
    private static readonly Pose Hanging = new(0.0, -0.2, 3.02, 0.05, 3.1, 0.02, 0.18, -0.3, 0.05, -0.15, 0, true);
    private static readonly Pose Working = new(0.12, 0.25, 1.15, 0.9, 1.0, 1.1, 0.05, -0.05, -0.05, -0.05);

    /// <summary>Half a walking stride (near leg forward to far leg forward); the other half is its mirror.</summary>
    private static readonly Pose[] WalkHalf =
    [
        new(0.04, 0, -0.32, 0.25, 0.32, 0.35, 0.44, -0.06, -0.32, -0.3),
        new(0.05, 0, -0.18, 0.28, 0.18, 0.3, 0.26, -0.32, -0.12, -0.78),
        new(0.04, 0, 0.0, 0.25, 0.0, 0.25, 0.0, -0.08, 0.2, -0.95),
        new(0.03, 0, 0.16, 0.25, -0.16, 0.28, -0.22, -0.04, 0.42, -0.45),
    ];

    private static readonly Pose[] RunHalf =
    [
        new(0.24, -0.1, -0.75, 1.45, 0.85, 1.25, 0.8, -0.25, -0.55, -0.6),
        new(0.26, -0.1, -0.4, 1.5, 0.45, 1.3, 0.32, -0.55, -0.28, -1.55),
        new(0.24, -0.1, 0.1, 1.55, -0.1, 1.45, -0.2, -0.3, 0.55, -1.75, 8),
        new(0.22, -0.1, 0.7, 1.4, -0.65, 1.3, -0.55, -0.3, 0.92, -0.85, 4),
    ];

    private static readonly Pose[] SwimCycle =
    [
        new(0, 0, 2.9, 0.1, 1.2, 0.4, 0.3, -0.4, -0.25, -0.2, 0, true),
        new(0, 0, 2.0, 0.3, 2.4, 0.2, 0.1, -0.15, 0.1, -0.5, 0, true),
        new(0, 0, 0.6, 0.5, 2.9, 0.1, -0.25, -0.2, 0.3, -0.4, 0, true),
        new(0, 0, 1.6, 0.2, 2.0, 0.3, 0.1, -0.5, 0.1, -0.15, 0, true),
    ];

    private static readonly Pose[] KickKeys =
    [
        Standing,
        new(-0.05, 0.05, 0.6, 0.5, -0.5, 0.4, 1.3, -1.9, -0.08, -0.06),
        new(-0.3, 0.1, 0.9, 0.3, -0.8, 0.3, 1.5, -0.08, -0.16, -0.04),
        new(-0.2, 0.05, 0.6, 0.4, -0.5, 0.4, 1.0, -1.2, -0.1, -0.05),
        Standing,
    ];

    private static Pose Cycle(Pose[] half, double phase)
    {
        phase -= Math.Floor(phase);
        int n = half.Length * 2;
        double at = phase * n;
        int i = (int)at;
        double k = Smooth(at - i);
        Pose Key(int j) { j %= n; return j < half.Length ? half[j] : half[j - half.Length].Mirror(); }
        return Pose.Lerp(Key(i), Key(i + 1), k);
    }

    private static Pose Keys(Pose[] keys, double k)
    {
        double at = Math.Clamp(k, 0, 1) * (keys.Length - 1);
        int i = Math.Min(keys.Length - 2, (int)at);
        return Pose.Lerp(keys[i], keys[i + 1], Smooth(at - i));
    }

    /// <summary>In the air: crouched to spring, tucked at the top, reaching for the ground coming down.</summary>
    private static Pose Airborne(double vy, bool hop)
    {
        var up = hop ? new Pose(0.05, -0.1, 2.4, 0.3, 2.2, 0.4, 0.5, -0.9, 0.35, -0.8, 0, true) : new Pose(0.28, -0.15, 1.5, 0.6, -0.7, 0.4, 0.95, -0.9, -0.62, -0.55, 0, true);
        var top = new Pose(0.16, 0, 1.05, 0.8, 0.3, 0.7, 1.15, -1.65, 0.35, -1.6, 12, true);
        var down = new Pose(0.04, 0.1, 1.9, 0.3, -0.5, 0.3, 0.6, -0.45, 0.15, -0.55, 0, true);
        double s = Math.Clamp((vy + 480) / 960, 0, 1);
        return s < 0.5 ? Pose.Lerp(up, top, Smooth(s * 2)) : Pose.Lerp(top, down, Smooth(s * 2 - 1));
    }

    // ───────────────────────────── a body's pose ─────────────────────────────

    /// <summary>Where and how to draw someone: their pose, the point on the ground under them, a turn of the whole body
    /// about a pivot, how wide (a turn round squeezes them through nothing), and how solid.</summary>
    private readonly record struct Placing(Pose Pose, double X, double Y, double Rot = 0, double PivotY = 0, double Squash = 1, double Fade = 1);

    private Placing Place(World w, Body b)
    {
        double t = b.T;
        double walkLen = b.Alien ? 160 : 130, runLen = 180;
        switch (b.Act)
        {
            case Act.Walk: return new(Cycle(WalkHalf, b.Stride / walkLen), b.X, b.Y);
            case Act.Run: return new(Cycle(RunHalf, b.Stride / runLen), b.X, b.Y);
            case Act.Turn:
            {
                double k = Math.Min(1, t / (b.Alien ? Sim.GuardTurn : Sim.TurnSeconds)), s = -Math.Cos(Math.PI * k);
                return new(b.Gun || b.Alien ? Aiming : Standing, b.X, b.Y, Squash: Math.Sign(s) * Math.Max(0.14, Math.Abs(s)));
            }
            case Act.Crouch: return new(Pose.Lerp(Standing, Crouching, Smooth(t / 0.12)), b.X, b.Y);
            case Act.Land:
                return new(Pose.Lerp(Pose.Lerp(Standing, Crouching, b.Dur > 0.15 ? 0.9 : 0.45), Standing, Smooth(t / Math.Max(0.05, b.Dur))), b.X, b.Y);
            case Act.Leap or Act.Hop: return new(Airborne(b.Vy, b.Act == Act.Hop), b.X, b.Y);
            case Act.Fall:
            {
                double f = Math.Sin(t * 14);
                return new(new Pose(0.05, -0.2, 2.6 + f * 0.3, 0.4, 2.2 - f * 0.3, 0.5, 0.5 + f * 0.2, -0.7, 0.1 - f * 0.2, -0.9, 0, true), b.X, b.Y);
            }
            case Act.Kick: return new(Keys(KickKeys, t / Sim.KickSeconds), b.X, b.Y);
            case Act.Aim: return new(Breathe(Aiming, t), b.X, b.Y);
            case Act.Charge: return new(Aiming with { Lean = -0.05 + Math.Sin(t * 40) * 0.008 }, b.X, b.Y);
            case Act.Recoil:
            {
                double k = Math.Sin(Math.PI * Math.Min(1, t / Sim.RecoilSeconds));
                return new(Aiming with { ArmN = 1.57 + 0.28 * k, ElbowN = 0.2 * k, Lean = -0.03 - 0.05 * k }, b.X, b.Y);
            }
            case Act.Cast:
            {
                double k = Smooth(t / Sim.CastSeconds);
                return new(Aiming with { ArmN = 2.5 - 1.0 * k, ElbowN = 0.1, Lean = 0.06 - 0.08 * k }, b.X, b.Y);
            }
            case Act.Swim:
            {
                var pose = Cycle(SwimCycle, b.Stride / 170 + w.Clock * 0.25);
                return new(pose, b.X, b.Y + 62, b.Tilt, -62);
            }
            case Act.Climb:
            {
                double k = Math.Min(1, t / Sim.ClimbSeconds);
                var pose = k < 0.5 ? Pose.Lerp(SwimCycle[0], Crouching, Smooth(k * 2)) : Pose.Lerp(Crouching, Standing, Smooth(k * 2 - 1));
                return new(pose, b.X + (b.Tx - b.X) * Smooth(k), b.Y + 62 + (b.Ty - b.Y - 62) * Smooth(k));
            }
            case Act.Swing:
            {
                var vine = w.RoomOf(b).Vine!.Value;
                double phi = Math.Asin(Math.Clamp((b.X - vine.X) / vine.Tip, -1, 1));
                return new(Hanging with { HipN = 0.25 + 0.3 * Math.Sin(t * 6), HipF = 0.1 + 0.3 * Math.Sin(t * 6 + 0.6) }, b.X, b.Y, -b.Facing * phi, -124);
            }
            case Act.Work: return new(Working with { ElbowN = 0.9 + 0.35 * Math.Sin(t * 7), ElbowF = 1.1 + 0.35 * Math.Sin(t * 6 + 1) }, b.X, b.Y);
            case Act.Caged: return new(Hanging with { Free = false, HipN = 0.05, HipF = -0.05, KneeN = -0.05, KneeF = -0.05, ArmN = 2.75, ArmF = 2.9 }, b.X, b.Y);
            case Act.Dead: return Dying(w, b);
        }
        return new(b.Gun && b.Alien ? Breathe(Aiming, t) : Breathe(Standing, t), b.X, b.Y);
    }

    private static Pose Breathe(Pose p, double t) => p with { Lean = p.Lean + Math.Sin(t * 2.2) * 0.012, ArmF = p.ArmF + Math.Sin(t * 2.2) * 0.02 };

    /// <summary>How each death looks: pulled under by the tentacles, limp in the water, or a collapse to the ground.</summary>
    private static Placing Dying(World w, Body b)
    {
        double t = b.T;
        switch (b.Cause)
        {
            case "the tentacles":
            {
                double k = Smooth(t / 1.4), f = Math.Sin(t * 9);
                var pose = new Pose(0, -0.3, 2.9 + f * 0.2, 0.2, 2.6 - f * 0.2, 0.3, 0.2, -0.3, -0.1, -0.2, 0, true);
                return new(pose, b.X, b.Y + 62 + k * 160, 0.1 * f, -62, Fade: 1 - Math.Max(0, k - 0.7) * 2);
            }
            case "drowning":
                return new(SwimCycle[1] with { ArmN = 1.2, ArmF = 1.6, Head = 0.5 }, b.X, b.Y + 62 + Smooth(t / 3) * 40, 1.45 + Smooth(t / 2) * 0.5, -62);
        }
        double fall = b.Cause == "a fall" ? 1 : Smooth(t / 0.75);
        var collapse = fall < 0.45 ? Pose.Lerp(Standing, Kneeling, Smooth(fall / 0.45)) : Pose.Lerp(Kneeling, Lying, Smooth((fall - 0.45) / 0.55));
        double rot = -Math.PI / 2 * Smooth((fall - 0.4) / 0.6);
        return new(collapse, b.X, b.Y - 9 * Smooth((fall - 0.5) / 0.5), rot, 0);
    }

    // ───────────────────────────── drawing a body ─────────────────────────────

    private void DrawBody(DrawingContext dc, World w, Body b)
    {
        switch (b.Kind)
        {
            case Kind.Beast: DrawBeast(dc, w, b); return;
            case Kind.Leech: DrawLeech(dc, w, b); return;
        }
        // A guard not yet walked in is off the screen.
        if (b.Alien && b.Kind != Kind.Friend && !b.Awake && b.X < 0) return;
        var place = Place(w, b);
        bool xray = b.Act == Act.Dead && b.Cause.Contains("shot") && b.T < 0.36;
        bool gun = b.Kind is Kind.Guard or Kind.Warden || b.Gun && b.Act is Act.Aim or Act.Charge or Act.Recoil or Act.Cast or Act.Turn && b.Kind == Kind.Hero && b.Holding;
        DrawPerson(dc, BuildOf(b.Kind), place, b.Facing, gun && b.Act != Act.Dead, xray, b.Kind == Kind.Hero ? b.Hold : b.Act == Act.Charge ? b.T * Sim.SuperHold / Sim.WardenCharge + 0.01 : 0,
            b.Act is Act.Charge);
    }

    /// <summary>The joints of a posed skeleton, in its own space: x forward, y down, the feet's ground at 0.</summary>
    private readonly record struct Rig(Point Hip, Point KneeN, Point AnkleN, Point KneeF, Point AnkleF, Point Neck, Point Shoulder,
        Point ElbowN, Point HandN, Point ElbowF, Point HandF, Point Head, double ShinN, double ShinF, double HeadTurn, double ForeN);

    private static Rig Pose2Rig(Build k, Pose p)
    {
        static Point Dir(double a, double len) => new(Math.Sin(a) * len, Math.Cos(a) * len);
        var tn = Dir(p.HipN, k.Thigh);
        var sn = Dir(p.HipN + p.KneeN, k.Shin);
        var tf = Dir(p.HipF, k.Thigh);
        var sf = Dir(p.HipF + p.KneeF, k.Shin);
        double drop = Math.Max(tn.Y + sn.Y, tf.Y + sf.Y);
        double hipY = p.Free ? -(k.Thigh + k.Shin) * 0.96 - p.Lift : -drop - p.Lift;
        var hip = new Point(0, hipY);
        var neck = new Point(hip.X + Math.Sin(p.Lean) * k.Torso, hip.Y - Math.Cos(p.Lean) * k.Torso);
        var shoulder = new Point(neck.X - Math.Sin(p.Lean) * 5, neck.Y + Math.Cos(p.Lean) * 5);
        double ha = p.Lean + p.Head;
        var head = new Point(neck.X + Math.Sin(ha) * (k.Neck + k.Head), neck.Y - Math.Cos(ha) * (k.Neck + k.Head));
        var en = shoulder + Dir(p.ArmN, k.Upper);
        var ef = shoulder + Dir(p.ArmF, k.Upper);
        return new Rig(hip, hip + tn, hip + tn + sn, hip + tf, hip + tf + sf, neck, shoulder, en, en + Dir(p.ArmN + p.ElbowN, k.Fore),
            ef, ef + Dir(p.ArmF + p.ElbowF, k.Fore), head, p.HipN + p.KneeN, p.HipF + p.KneeF, ha, p.ArmN + p.ElbowN);
    }

    /// <summary>
    /// Draws a posed body: far arm, far leg, near leg, the body, the head, the near arm (holding the pistol), each a
    /// flat polygon. Shot, for a moment the body flashes white with its bones showing through.
    /// </summary>
    private static void DrawPerson(DrawingContext dc, Build k, Placing at, int facing, bool gun, bool xray, double charge, bool super)
    {
        var p = at.Pose;
        var r = Pose2Rig(k, p);
        double cos = Math.Cos(at.Rot), sin = Math.Sin(at.Rot), f = facing * at.Squash;
        Point M(Point q)
        {
            double lx = q.X, ly = q.Y - at.PivotY;
            double rx = lx * cos - ly * sin, ry = lx * sin + ly * cos + at.PivotY;
            return new Point(at.X + f * rx, at.Y + ry);
        }
        uint C(uint c) => xray ? (c == k.Skin || c == k.SkinDark ? 0xFFF4F8FF : 0xFFE0ECFF) : at.Fade < 1 ? Alpha(c, at.Fade) : c;
        void Seg(Point a, Point b, double wa, double wb, uint c)
        {
            var (ma, mb) = (M(a), M(b));
            double dx = mb.X - ma.X, dy = mb.Y - ma.Y, len = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy)), nx = -dy / len, ny = dx / len;
            Poly(dc, C(c), [new(ma.X + nx * wa / 2, ma.Y + ny * wa / 2), new(mb.X + nx * wb / 2, mb.Y + ny * wb / 2), new(mb.X - nx * wb / 2, mb.Y - ny * wb / 2), new(ma.X - nx * wa / 2, ma.Y - ny * wa / 2)]);
            Ngon(dc, C(c), mb.X, mb.Y, wb / 2, wb / 2, 6);
        }
        void Foot(Point ankle, double shin, uint c)
        {
            double a = -0.6 * shin, ca = Math.Cos(a), sa = Math.Sin(a);
            Point F(double x, double y) => M(new Point(ankle.X + x * ca - y * sa, ankle.Y + x * sa + y * ca));
            Poly(dc, C(c), [F(-5, -6), F(5, -6), F(15, 0), F(15, 4), F(-5, 4)]);
        }
        void Leg(Point knee, Point ankle, double shin, uint c, uint shoe)
        {
            Seg(r.Hip, knee, k.LegW + 1, k.LegW - 1, c);
            Seg(knee, ankle, k.LegW - 1, k.LegW - 3, c);
            Foot(ankle, shin, shoe);
        }
        void Arm(Point elbow, Point hand, uint sleeve, uint skin, double sleeveTo)
        {
            Seg(r.Shoulder, elbow, k.ArmW + 1, k.ArmW, sleeve);
            var mid = new Point(elbow.X + (hand.X - elbow.X) * sleeveTo, elbow.Y + (hand.Y - elbow.Y) * sleeveTo);
            if (sleeveTo > 0) Seg(elbow, mid, k.ArmW, k.ArmW - 1, sleeve);
            Seg(sleeveTo > 0 ? mid : elbow, hand, k.ArmW - 1, k.ArmW - 2, skin);
            var mh = M(hand);
            Ngon(dc, C(skin), mh.X, mh.Y, k.ArmW * 0.62, k.ArmW * 0.62, 6);
        }
        // The far side, in shadow.
        bool sleeves = !k.Alien || k.Armour;
        Arm(r.ElbowF, r.HandF, k.Alien && !k.Armour ? k.SkinDark : k.TopDark, k.SkinDark, sleeves ? (k.Armour ? 0.0 : 0.85) : 0);
        Leg(r.KneeF, r.AnkleF, r.ShinF, k.LegsDark, Mix(k.Feet, 0xFF000000, 0.4));
        Leg(r.KneeN, r.AnkleN, r.ShinN, k.Legs, k.Feet);
        // The body: a polygon along the spine, broader at the chest.
        double ux = r.Neck.X - r.Hip.X, uy = r.Neck.Y - r.Hip.Y, ul = Math.Sqrt(ux * ux + uy * uy);
        double nx = -uy / ul, ny = ux / ul;
        Point A(double along, double across) => M(new Point(r.Hip.X + ux * along + nx * across, r.Hip.Y + uy * along + ny * across));
        double hw = k.HipW / 2, cw = k.ChestW / 2;
        Poly(dc, C(k.Top), [A(-0.08, hw), A(0.45, cw * 0.95), A(0.78, cw * 1.12), A(1.0, cw * 0.7), A(1.04, -cw * 0.5), A(0.7, -cw * 0.95), A(0.3, -hw * 1.05), A(-0.08, -hw)]);
        if (k.Armour)
        {
            Poly(dc, C(k.TopDark), [A(0.25, -cw * 0.98), A(0.7, -cw * 0.95), A(1.04, -cw * 0.5), A(0.95, -cw * 0.1), A(0.55, -cw * 0.3)]);
            Poly(dc, C(k.Trim), [A(-0.02, hw * 1.02), A(0.08, hw * 1.02), A(0.08, -hw * 1.06), A(-0.02, -hw * 1.06)]);
            Poly(dc, C(Mix(k.Top, 0xFFFFFFFF, 0.18)), [A(0.6, cw * 1.0), A(0.78, cw * 1.12), A(0.84, cw * 0.4), A(0.66, cw * 0.4)]);
        }
        else if (k.Alien)
        {
            // A wrap about the hips.
            Poly(dc, C(k.Legs), [A(-0.12, hw * 1.15), A(0.22, hw * 1.1), A(0.25, -hw * 1.15), A(-0.12, -hw * 1.15)]);
            Poly(dc, C(k.Trim), [A(0.17, hw * 1.12), A(0.25, hw * 1.1), A(0.27, -hw * 1.15), A(0.19, -hw * 1.15)]);
        }
        else
        {
            // Shirt: a shadowed back, the belt, the open collar.
            Poly(dc, C(k.TopDark), [A(0.0, -hw), A(0.3, -hw * 1.05), A(0.7, -cw * 0.95), A(0.92, -cw * 0.75), A(0.55, -cw * 0.55), A(0.2, -hw * 0.6)]);
            Poly(dc, C(k.Trim), [A(-0.06, hw * 1.02), A(0.04, hw * 1.02), A(0.04, -hw * 1.04), A(-0.06, -hw * 1.04)]);
            Poly(dc, C(k.Skin), [A(0.93, cw * 0.62), A(1.02, cw * 0.66), A(1.0, cw * 0.15)]);
        }
        // Neck and head.
        Seg(r.Neck, new Point(r.Neck.X + (r.Head.X - r.Neck.X) * 0.45, r.Neck.Y + (r.Head.Y - r.Neck.Y) * 0.45), k.Head * 0.85, k.Head * 0.8, k.SkinDark);
        DrawHead(dc, k, r.Head, r.HeadTurn, M, C);
        // The near arm, with the pistol.
        Arm(r.ElbowN, r.HandN, k.Alien && !k.Armour ? k.Skin : k.Top, k.Skin, sleeves ? (k.Armour ? 0.0 : 0.85) : 0);
        if (k.Armour) Seg(r.Shoulder, new Point(r.Shoulder.X + (r.ElbowN.X - r.Shoulder.X) * 0.45, r.Shoulder.Y + (r.ElbowN.Y - r.Shoulder.Y) * 0.45), k.ArmW + 9, k.ArmW + 5, Mix(k.Top, 0xFFFFFFFF, 0.12));
        if (gun)
        {
            double a = r.ForeN;
            Point G(double along, double across) => M(new Point(r.HandN.X + Math.Sin(a) * along + Math.Cos(a) * across, r.HandN.Y + Math.Cos(a) * along - Math.Sin(a) * across));
            Poly(dc, C(0xFF1C1C22), [G(-4, -4), G(18, -3.5), G(18, 3), G(-4, 4)]);
            Poly(dc, C(0xFF5E6676), [G(2, -3.5), G(16, -3), G(16, -1), G(2, -1)]);
            Poly(dc, C(0xFF1C1C22), [G(-2, 2), G(3, 2), G(1, 10), G(-4, 9)]);
            if (charge > Sim.ShieldHold * 0.5 || super)
            {
                var m = G(21, 0);
                bool big = charge >= Sim.SuperHold || super && charge >= Sim.SuperHold * 0.95;
                double rad = big ? 13 + Math.Sin(charge * 50) * 2.5 : 4 + 8 * Math.Clamp((charge - 0.15) / (Sim.SuperHold - 0.15), 0, 1);
                uint glow = big ? 0xFFFFF4C8 : 0xFFB8F0FF;
                Ngon(dc, Alpha(glow, 0.25), m.X, m.Y, rad * 2.2, rad * 2.2, 12, charge * 7);
                Ngon(dc, Alpha(glow, 0.55), m.X, m.Y, rad * 1.35, rad * 1.35, 10, -charge * 5);
                Ngon(dc, 0xFFFFFFFF, m.X, m.Y, rad * 0.7, rad * 0.7, 8);
            }
        }
        if (xray)
        {
            // The bones, through the flash.
            uint bone = 0xFF20283A;
            void Bone(Point a, Point b) => Strip(dc, bone, M(a).X, M(a).Y, M(b).X, M(b).Y, 3.2, 3.2);
            Bone(r.Hip, r.KneeN); Bone(r.KneeN, r.AnkleN); Bone(r.Hip, r.KneeF); Bone(r.KneeF, r.AnkleF);
            Bone(r.Hip, r.Neck); Bone(r.Shoulder, r.ElbowN); Bone(r.ElbowN, r.HandN); Bone(r.Shoulder, r.ElbowF); Bone(r.ElbowF, r.HandF);
            for (int i = 1; i <= 3; i++)
            {
                var c0 = new Point(r.Hip.X + (r.Neck.X - r.Hip.X) * (0.45 + i * 0.13), r.Hip.Y + (r.Neck.Y - r.Hip.Y) * (0.45 + i * 0.13));
                Bone(new Point(c0.X - 9, c0.Y + 2), new Point(c0.X + 9, c0.Y + 2));
            }
            var hc = M(r.Head);
            Ngon(dc, bone, hc.X, hc.Y, k.Head * 0.8, k.Head * 0.8, 8);
            Ngon(dc, 0xFFF4F8FF, hc.X + facing * 3, hc.Y - 1, k.Head * 0.32, k.Head * 0.28, 6);
        }
    }

    /// <summary>A head in profile: the physicist's, with his dark hair and spectacles; an alien's, long at the back.</summary>
    private static void DrawHead(DrawingContext dc, Build k, Point centre, double turn, Func<Point, Point> m, Func<uint, uint> c, bool close = false, double jaw = 0)
    {
        double rr = k.Head, ca = Math.Cos(turn), sa = Math.Sin(turn);
        Point H(double x, double y) => m(new Point(centre.X + (x * ca - y * sa) * rr, centre.Y + (x * sa + y * ca) * rr));
        if (!k.Alien)
        {
            Poly(dc, c(k.Skin), [H(-1.0, -0.1), H(-0.7, -0.85), H(0, -1.05), H(0.75, -0.6), H(0.95, -0.15), H(1.22, 0.18), H(0.95, 0.36), H(0.92, 0.55), H(0.8, 0.88), H(0.1, 0.95), H(-0.35, 0.75)]);
            Poly(dc, c(k.SkinDark), [H(-0.35, 0.75), H(0.1, 0.95), H(0.8, 0.88), H(0.55, 0.6), H(-0.1, 0.5)]);
            Poly(dc, c(k.Hair), [H(-1.1, 0.25), H(-1.0, -0.72), H(-0.35, -1.18), H(0.5, -1.1), H(0.95, -0.62), H(0.62, -0.5), H(0.15, -0.62), H(-0.25, -0.25), H(-0.5, 0.42)]);
            Poly(dc, c(k.SkinDark), [H(-0.42, -0.05), H(-0.18, -0.12), H(-0.12, 0.25), H(-0.36, 0.3)]);
            Poly(dc, c(0xFF1A1414), [H(0.5, -0.24), H(0.68, -0.24), H(0.66, -0.1), H(0.5, -0.1)]);
            // Spectacles.
            Poly(dc, c(0xFF2A2A30), [H(0.36, -0.36), H(0.86, -0.36), H(0.86, -0.3), H(0.36, -0.3)]);
            Poly(dc, c(0x7090C0E0), [H(0.42, -0.32), H(0.84, -0.32), H(0.8, 0.02), H(0.44, 0.02)]);
            Poly(dc, c(0xFF2A2A30), [H(-0.2, -0.34), H(0.4, -0.34), H(0.4, -0.29), H(-0.2, -0.29)]);
            if (close)
            {
                // Close up: the brow, the nostril, the mouth, the ear's curl.
                Poly(dc, c(k.Hair), [H(0.4, -0.5), H(0.85, -0.5), H(0.82, -0.43), H(0.42, -0.42)]);
                Poly(dc, c(k.SkinDark), [H(1.0, 0.16), H(1.12, 0.2), H(1.0, 0.24)]);
                Poly(dc, c(Mix(k.SkinDark, 0xFF000000, 0.25)), [H(0.78, 0.5), H(0.96, 0.47), H(0.95, 0.51), H(0.8, 0.54)]);
                Poly(dc, c(Mix(k.SkinDark, 0xFF000000, 0.2)), [H(-0.36, -0.02), H(-0.24, -0.06), H(-0.2, 0.18), H(-0.32, 0.22)]);
            }
            return;
        }
        Poly(dc, c(k.Skin), [H(-1.6, -0.5), H(-1.25, -1.05), H(-0.35, -1.18), H(0.55, -0.9), H(0.95, -0.35), H(1.08, 0.05), H(0.9, 0.38), H(0.86, 0.6), H(0.72, 0.98), H(0.0, 1.0), H(-0.5, 0.65), H(-1.2, 0.05)]);
        Poly(dc, c(k.SkinDark), [H(-1.6, -0.5), H(-1.2, 0.05), H(-0.5, 0.65), H(0.0, 1.0), H(0.72, 0.98), H(0.4, 0.62), H(-0.3, 0.3), H(-1.0, -0.15)]);
        if (k.Armour)
        {
            // A crest along the skull.
            Poly(dc, c(k.Trim), [H(-1.5, -0.62), H(-1.15, -1.18), H(-0.3, -1.32), H(0.42, -1.0), H(0.3, -0.88), H(-0.35, -1.06), H(-1.05, -0.95)]);
        }
        Poly(dc, c(Mix(k.SkinDark, 0xFF000000, 0.3)), [H(0.3, -0.42), H(0.98, -0.36), H(0.9, -0.22), H(0.3, -0.24)]);
        Poly(dc, c(k.Armour ? 0xFFFFC040 : 0xFF1A1810), [H(0.5, -0.2), H(0.8, -0.2), H(0.76, -0.06), H(0.52, -0.08)]);
        Poly(dc, c(Mix(k.SkinDark, 0xFF000000, 0.4)), [H(0.62, 0.52), H(0.9, 0.5), H(0.88, 0.58 + jaw * 0.25), H(0.62, 0.6 + jaw * 0.12)]);
        if (close)
        {
            Poly(dc, c(Mix(k.SkinDark, 0xFF000000, 0.2)), [H(-0.9, -0.6), H(-0.4, -0.95), H(-0.36, -0.86), H(-0.84, -0.5)]);
            Poly(dc, c(Mix(k.SkinDark, 0xFF000000, 0.2)), [H(-0.7, -0.2), H(-0.2, -0.5), H(-0.16, -0.42), H(-0.64, -0.1)]);
            Poly(dc, c(0xFFFFFFFF), [H(0.6, -0.17), H(0.66, -0.17), H(0.65, -0.12), H(0.6, -0.12)]);
        }
    }

    // ───────────────────────────── the beast ─────────────────────────────

    private void DrawBeast(DrawingContext dc, World w, Body b)
    {
        if (!b.Awake) return;
        double phase = b.Stride / 210 * Math.PI * 2;
        bool running = b.Act == Act.Run;
        double rot = b.Act == Act.Leap ? -0.35 * Math.Sin(Math.PI * Math.Min(1, b.T / Sim.BeastRise)) : 0;
        double roar = b.Act is Act.Turn or Act.Stand ? 0.5 + 0.5 * Math.Sin(b.T * 5) : running ? 0.25 + 0.15 * Math.Sin(phase * 2) : 0.6;
        DrawBeastAt(dc, b.X, b.Y, b.Facing, running ? phase : b.T * 2, rot, roar, running ? 1 : 0.2, 1);
    }

    /// <summary>The beast: a great black hunched thing on four legs, with back-swept horns and a red eye.</summary>
    private static void DrawBeastAt(DrawingContext dc, double x, double y, int facing, double phase, double rot, double roar, double gallop, double scale)
    {
        double bob = -Math.Abs(Math.Sin(phase)) * 7 * gallop, cos = Math.Cos(rot), sin = Math.Sin(rot);
        Point M(double lx, double ly)
        {
            ly += bob;
            double rx = lx * cos - (ly + 60) * sin, ry = lx * sin + (ly + 60) * cos - 60;
            return new Point(x + facing * rx * scale, y + ry * scale);
        }
        void Pg(uint c, params double[] xy)
        {
            var pts = new Point[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = M(xy[i * 2], xy[i * 2 + 1]);
            Poly(dc, c, pts);
        }
        void Limb(double hx, double hy, double a, double bend, uint c, double w0)
        {
            double kx = hx + Math.Sin(a) * 34, ky = hy + Math.Cos(a) * 34;
            double fx = kx + Math.Sin(a + bend) * 36, fy = ky + Math.Cos(a + bend) * 36;
            fy = Math.Min(fy, 0 - bob);
            var (h0, k0, f0) = (M(hx, hy), M(kx, ky), M(fx, fy));
            Strip(dc, c, h0.X, h0.Y, k0.X, k0.Y, w0 * scale, w0 * 0.7 * scale);
            Strip(dc, c, k0.X, k0.Y, f0.X, f0.Y, w0 * 0.7 * scale, w0 * 0.45 * scale);
            Pg(c, fx - 6, fy - 4, fx + 14, fy - 3, fx + 16, fy + 1, fx - 6, fy + 1);
        }
        uint body = 0xFF16101A, far = 0xFF0A070C, rim = 0xFF41364C, belly = 0xFF231A29;
        double g = gallop;
        // The far legs.
        Limb(38, -58, 0.7 * Math.Sin(phase + 0.6) * g, -0.6 * Math.Max(0, Math.Cos(phase + 0.6)) * g - 0.1, far, 18);
        Limb(-62, -60, 0.8 * Math.Sin(phase + Math.PI + 0.6) * g, 0.7 * Math.Max(0, Math.Cos(phase + Math.PI + 0.6)) * g + 0.15, far, 22);
        // The tail.
        double tw = Math.Sin(phase * 0.5) * 12;
        Pg(far, -70, -84, -112, -100 + tw, -150, -84 + tw * 1.4, -160, -70 + tw * 1.6, -146, -76 + tw * 1.3, -110, -86 + tw * 0.6, -70, -70);
        // The body, hunched at the shoulders.
        Pg(body, -84, -78, -58, -96, -10, -100, 30, -112, 56, -104, 66, -84, 52, -50, 18, -44, -24, -52, -62, -50, -84, -60);
        Pg(belly, 52, -50, 18, -44, -24, -52, -62, -50, -40, -62, 10, -58, 40, -62);
        Pg(rim, -58, -96, -10, -100, 30, -112, 56, -104, 46, -100, 26, -104, -10, -94, -52, -90);
        // The head, jaws parted by the roar.
        double jaw = 6 + roar * 16;
        Pg(body, 50, -100, 84, -102, 112, -96, 138, -86, 134, -76, 100, -74, 70, -66);
        Pg(body, 70, -70, 100, -72, 132, -72 + jaw * 0.5, 126, -64 + jaw, 96, -60 + jaw * 0.6, 70, -60);
        Pg(0xFF6A1A1E, 100, -74, 132, -74, 128, -68 + jaw * 0.5, 100, -68 + jaw * 0.3);
        for (int i = 0; i < 4; i++)
        {
            double tx = 104 + i * 7;
            Pg(0xFFE8E0D0, tx, -75, tx + 4, -75, tx + 2, -70);
            Pg(0xFFE8E0D0, tx, -66 + jaw * 0.45, tx + 4, -66 + jaw * 0.45, tx + 2, -71 + jaw * 0.45);
        }
        Pg(0xFFE84830, 104, -94, 114, -92, 112, -89, 103, -90);
        Pg(far, 82, -102, 70, -122, 40, -134, 58, -120, 66, -104);
        Pg(rim, 92, -101, 84, -124, 58, -140, 76, -122, 80, -102);
        // The near legs.
        Limb(40, -60, 0.7 * Math.Sin(phase) * g, -0.6 * Math.Max(0, Math.Cos(phase)) * g - 0.1, body, 20);
        Limb(-60, -62, 0.8 * Math.Sin(phase + Math.PI) * g, 0.7 * Math.Max(0, Math.Cos(phase + Math.PI)) * g + 0.15, body, 24);
    }

    // ───────────────────────────── leeches and tentacles ─────────────────────────────

    private void DrawLeech(DrawingContext dc, World w, Body b)
    {
        double x = b.X, y = b.Y;
        uint body = 0xFF1C1610, back = 0xFF54432C, mouth = 0xFFC8B090;
        if (b.Act == Act.Dead)
        {
            P(dc, 0xFF2A2214, x - 22, y, x - 12, y - 4, x + 6, y - 5, x + 22, y - 1, x + 20, y + 1, x - 20, y + 1);
            P(dc, 0xFF6A7A30, x - 10, y - 2, x - 2, y - 4, x + 10, y - 3, x + 4, y);
            return;
        }
        if (b.Act is Act.Stand or Act.Fall)
        {
            // Hanging (or dropping): a drip-shaped blob, head down.
            double s = b.Act == Act.Stand ? 1 + Math.Sin(w.Clock * 3 + x) * 0.08 : 1.15;
            if (b.Act == Act.Stand) Strip(dc, 0x80302418, x, y - 30, x, y - 8, 2, 2);
            P(dc, body, x - 6, y - 10, x + 6, y - 10, x + 9, y + 10 * s, x + 4, y + 22 * s, x - 4, y + 22 * s, x - 9, y + 10 * s);
            P(dc, back, x - 2, y - 8, x + 4, y - 8, x + 6, y + 8 * s, x + 1, y + 16 * s);
            P(dc, mouth, x - 3, y + 20 * s, x + 3, y + 20 * s, x, y + 24 * s);
            return;
        }
        double st = 1 + 0.2 * Math.Sin(b.Stride * 0.35), f = b.Facing;
        double L(double v) => x + f * v * st;
        P(dc, body, L(-18), y, L(-15), y - 8 / st, L(-4), y - 13 / st, L(8), y - 12 / st, L(17), y - 7 / st, L(21), y);
        P(dc, back, L(-12), y - 9 / st, L(-3), y - 12 / st, L(8), y - 11 / st, L(12), y - 8 / st, L(0), y - 8 / st);
        P(dc, mouth, L(18), y - 4, L(22), y - 1, L(18), y);
    }

    private static readonly double[] TentacleRoots = [345, 415, 485, 560, 630, 690];

    /// <summary>The tentacles of the pool, rising from the bottom as they wake, swaying, wrapping round the hero if they catch him.</summary>
    private void DrawTentacles(DrawingContext dc, World w, bool front)
    {
        var h = w.Hero;
        bool caught = h.Act == Act.Dead && h.Cause == "the tentacles";
        for (int i = 0; i < TentacleRoots.Length; i++)
        {
            bool reach = caught && i is 2 or 3;
            if (reach != front) continue;
            double bx = TentacleRoots[i], rise = Math.Max(0, w.Tentacles - Sim.TentacleDepth) * (0.8 + 0.25 * Rnd(i, 5)) + 26;
            double tipX = bx + Math.Sin(_clock * 1.6 + i * 1.7) * 26 * Math.Min(1, rise / 120), tipY = Field.Height - rise;
            if (reach)
            {
                var place = Dying(w, h);
                tipX = h.X + (i == 2 ? -14 : 14);
                tipY = place.Y - 70;
                bx = h.X + (i == 2 ? -60 : 60);
            }
            const int n = 10;
            var left = new List<Point>();
            var right = new List<Point>();
            for (int s = 0; s <= n; s++)
            {
                double k = s / (double)n, cx = bx + (tipX - bx) * k * k + Math.Sin(_clock * 2.3 + i + k * 5) * 10 * k, cy = Field.Height + 10 - (Field.Height + 10 - tipY) * k;
                double wd = 26 * (1 - k) + 3;
                left.Add(new Point(cx - wd / 2, cy));
                right.Insert(0, new Point(cx + wd / 2, cy));
            }
            Poly(dc, 0xFF1C4630, [.. left, .. right]);
            var inner = new List<Point>();
            for (int s = 0; s <= n; s++)
            {
                double k = s / (double)n, cx = bx + (tipX - bx) * k * k + Math.Sin(_clock * 2.3 + i + k * 5) * 10 * k, cy = Field.Height + 10 - (Field.Height + 10 - tipY) * k;
                inner.Add(new Point(cx + (26 * (1 - k) + 3) * 0.1, cy));
            }
            for (int s = n; s >= 0; s--)
            {
                double k = s / (double)n, cx = bx + (tipX - bx) * k * k + Math.Sin(_clock * 2.3 + i + k * 5) * 10 * k, cy = Field.Height + 10 - (Field.Height + 10 - tipY) * k;
                inner.Add(new Point(cx + (26 * (1 - k) + 3) * 0.42, cy));
            }
            Poly(dc, 0xFF3C7A52, inner);
            for (int s = 1; s < n; s += 2)
            {
                double k = s / (double)n, cx = bx + (tipX - bx) * k * k + Math.Sin(_clock * 2.3 + i + k * 5) * 10 * k, cy = Field.Height + 10 - (Field.Height + 10 - tipY) * k;
                Ngon(dc, 0xFF9AC8A0, cx - (26 * (1 - k) + 3) * 0.25, cy, 2.6 * (1.2 - k), 2.6 * (1.2 - k), 6);
            }
        }
    }
}

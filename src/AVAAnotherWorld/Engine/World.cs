namespace AVAAnotherWorld.Engine;

/// <summary>A small random number generator whose state can be copied.</summary>
public struct Dice
{
    private ulong _s;
    public Dice(int seed) => _s = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;

    public double Next()
    {
        _s ^= _s << 13;
        _s ^= _s >> 7;
        _s ^= _s << 17;
        return (_s >> 11) * (1.0 / (1UL << 53));
    }

    public int Next(int n) => (int)(Next() * n);
}

/// <summary>
/// The controls: left or right, up (jump; swim up), down (crouch; swim down), fire (kick; with the pistol, a tap shoots,
/// a hold makes a shield, a longer hold a super shot) and run.
/// </summary>
public readonly record struct Controls(int Dx = 0, bool Up = false, bool Down = false, bool Fire = false, bool Run = false);

/// <summary>Who someone is: the physicist, an alien guard, the warden, the friend, the beast, a leech.</summary>
public enum Kind { Hero, Guard, Warden, Friend, Beast, Leech }

/// <summary>Whose a shot or a shield is. A shield stops the other side's shots (and their walking).</summary>
public enum Side { Hero, Alien }

/// <summary>What someone is doing. Each act has its own pose and the time into it, which the screen animates.</summary>
public enum Act
{
    Stand, Walk, Run, Turn, Crouch, Leap, Hop, Fall, Land, Kick,
    /// <summary>The pistol up; held fire charges it.</summary>
    Aim, Charge, Recoil, Cast,
    Swim, Climb, Swing, Work, Caged,
    Dead,
}

/// <summary>Someone in the world: where (room, feet), which way, doing what, and for how long.</summary>
public sealed class Body
{
    public Kind Kind;
    public int Room;
    /// <summary>The feet on the ground (in the water, the middle of the body).</summary>
    public double X, Y;
    public double Vx, Vy;
    public int Facing = 1;
    public Act Act;
    /// <summary>Seconds into the act, and how long it lasts (for those with a length of their own).</summary>
    public double T, Dur;
    /// <summary>Where a climb out of the water ends; where on the vine the swing began.</summary>
    public double Tx, Ty;
    /// <summary>How far the legs have carried him, for the walking and running cycle.</summary>
    public double Stride;
    /// <summary>Seconds the fire button has been held: the pistol's charge.</summary>
    public double Hold;
    public bool Holding, Gun, Struck;
    public double Breath = Sim.BreathSeconds;
    /// <summary>How far over a swimmer is leaning: 0 upright, about 1.5 flat out ahead (turned smoothly, for the eye).</summary>
    public double Tilt = 0.3;
    /// <summary>The highest point of a jump or fall (the smallest Y), to judge the landing by.</summary>
    public double Top;
    /// <summary>A foe's time to its next shot; its delay before it comes on; its quickness to shield; its time between shots.</summary>
    public double Cool, Wake, Skill, Gap;
    public bool Awake;
    /// <summary>Where a guard walks in to; where a leech drops (once the hero passes it).</summary>
    public double To = double.NaN;
    public double Since;
    public string Cause = "";

    public Body Copy() => (Body)MemberwiseClone();
    public bool Alive => Act != Act.Dead;
    public Side Side => Kind is Kind.Hero or Kind.Friend ? Side.Hero : Side.Alien;
    public bool Alien => Kind is Kind.Guard or Kind.Warden or Kind.Friend;
    /// <summary>How tall the body stands, feet to crown.</summary>
    public double Height => Kind switch
    {
        Kind.Leech => 24,
        Kind.Beast => 110,
        Kind.Guard or Kind.Friend => 150,
        Kind.Warden => 164,
        _ => Act is Act.Crouch ? 78 : 124,
    };
    public bool OnGround => Act is not (Act.Leap or Act.Hop or Act.Fall or Act.Swim or Act.Swing or Act.Climb or Act.Caged or Act.Dead);
}

/// <summary>A laser bolt: whose, where, which way. A super shot breaks every shield it meets.</summary>
public sealed class Bolt
{
    public Side Side;
    public int Room;
    public double X, Y;
    public int Dir;
    public bool Super, Gone, Seen;
    public double Age;
    public Bolt Copy() => (Bolt)MemberwiseClone();
}

/// <summary>A shield wall standing on the floor, as tall as a guard, for a few seconds.</summary>
public sealed class Shield
{
    public Side Side;
    public int Room;
    public double X, Y;
    public double Life, Age;
    /// <summary>How recently a shot splashed against it (for the flicker).</summary>
    public double Struck;
    public Shield Copy() => (Shield)MemberwiseClone();
}

/// <summary>A flash on the screen: a bolt stopped by a shield, a splash, sparks, a squashed leech.</summary>
public sealed record Spark(int Room, double X, double Y, double Born, SparkKind Kind);

public enum SparkKind { Block, Hit, Splash, Sparks, Squish, Burst, Dust }

/// <summary>The cage of section three: its swing on the chain, and its fall.</summary>
public sealed class Cage
{
    public double Theta, Omega;
    public bool Broken, Landed, Crushed;
    public double X, Y, Vx, Vy, Since, Peak;
    public Cage Copy() => (Cage)MemberwiseClone();
}

/// <summary>
/// Everything in the current section that moves or changes, kept together so it can be copied whole: the hero, the
/// foes and the friend, the bolts and shields, the cage, the pool's tentacles, the door, the switch, the time.
/// </summary>
public sealed class World
{
    public SectionDef Def = null!;
    public Body Hero = new();
    public List<Body> Foes = [];
    public Body? Friend;
    public List<Bolt> Bolts = [];
    public List<Shield> Shields = [];
    public List<Spark> Sparks = [];
    public Cage Cage = new();
    /// <summary>Seconds since the checkpoint.</summary>
    public double Clock;
    /// <summary>How far the pool's tentacles have risen from the bottom.</summary>
    public double Tentacles;
    /// <summary>The friend's work on the door (seconds); it opens at <see cref="Sim.DoorSeconds"/>.</summary>
    public double Door;
    /// <summary>Seconds the switched barrier stays off.</summary>
    public double Switch;
    /// <summary>When the hero came into the room he's in.</summary>
    public double Entered;
    /// <summary>When the warden last began a super shot.</summary>
    public double LastSuper = -10;
    public Dice Dice = new(1);
    public bool Done;
    /// <summary>Seconds the section's goal has been met (it's over a moment later).</summary>
    public double Met = -1;
    public string Cause = "";
    public List<Sfx> Heard = [];

    public World Copy() => new()
    {
        Def = Def, Hero = Hero.Copy(), Foes = Foes.Select(f => f.Copy()).ToList(), Friend = Friend?.Copy(),
        Bolts = Bolts.Select(b => b.Copy()).ToList(), Shields = Shields.Select(s => s.Copy()).ToList(), Sparks = [.. Sparks],
        Cage = Cage.Copy(), Clock = Clock, Tentacles = Tentacles, Door = Door, Switch = Switch, Entered = Entered, LastSuper = LastSuper,
        Dice = Dice, Done = Done, Met = Met, Cause = Cause,
    };

    public int Section => Def.Number;
    public Room Here => Def.Rooms[Hero.Room];
    public Room RoomOf(Body b) => Def.Rooms[b.Room];
    public bool DoorOpen => Door >= Sim.DoorSeconds;
}

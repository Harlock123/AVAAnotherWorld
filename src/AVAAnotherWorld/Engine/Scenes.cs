namespace AVAAnotherWorld.Engine;

/// <summary>The cut-scenes: the experiment, the capture, the cage's fall, the friend on the bridge, the end.</summary>
public enum SceneId { Intro, Captured, Freed, Reunion, Ending }

/// <summary>One shot of a cut-scene, drawn by the screen in the same polygons as the game.</summary>
public enum ShotKind
{
    // The experiment.
    LabNight, Console, Accelerator, Strike, Surge, EmptyLab,
    // Captured.
    BeastLeap, GuardsFire, Silhouettes, Stun,
    // Freed.
    CageCrash, OruFace, Pistol,
    // The friend again.
    BridgeWave, TowerPoint,
    // The end.
    Wounded, Lifted, Wings, Flight, TheEnd,
}

/// <summary>A shot: what it shows, how long, and the sounds it asks for along the way.</summary>
public sealed record Shot(ShotKind Kind, double Seconds, params (double At, Sfx Sound)[] Cues);

/// <summary>Every cut-scene's shots, in order.</summary>
public static class SceneBook
{
    public static IReadOnlyList<Shot> Shots(SceneId id) => id switch
    {
        SceneId.Intro =>
        [
            new Shot(ShotKind.LabNight, 5.5, (0, Sfx.Rain), (0.7, Sfx.Thunder), (3.6, Sfx.Thunder)),
            new Shot(ShotKind.Console, 4.2, (0.3, Sfx.Typing), (2.2, Sfx.Typing)),
            new Shot(ShotKind.Accelerator, 3.6, (0.2, Sfx.Surge)),
            new Shot(ShotKind.Strike, 2.0, (0, Sfx.Thunder)),
            new Shot(ShotKind.Surge, 2.8, (0, Sfx.Surge)),
            new Shot(ShotKind.EmptyLab, 2.8, (0.3, Sfx.Hush)),
        ],
        SceneId.Captured =>
        [
            new Shot(ShotKind.BeastLeap, 1.8, (0, Sfx.Roar)),
            new Shot(ShotKind.GuardsFire, 2.4, (0.3, Sfx.Shot), (0.7, Sfx.Shot), (1.1, Sfx.Thud)),
            new Shot(ShotKind.Silhouettes, 3.6, (0, Sfx.Captured), (1.4, Sfx.Shout)),
            new Shot(ShotKind.Stun, 2.0, (0.3, Sfx.Zap)),
        ],
        SceneId.Freed =>
        [
            new Shot(ShotKind.CageCrash, 1.6, (0, Sfx.Crash)),
            new Shot(ShotKind.OruFace, 3.4, (0, Sfx.Freed), (0.5, Sfx.Oru)),
            new Shot(ShotKind.Pistol, 2.8, (1.0, Sfx.Charge)),
        ],
        SceneId.Reunion =>
        [
            new Shot(ShotKind.BridgeWave, 3.4, (0, Sfx.Reunion), (0.5, Sfx.Oru)),
            new Shot(ShotKind.TowerPoint, 3.2, (0.6, Sfx.Oru)),
        ],
        SceneId.Ending =>
        [
            new Shot(ShotKind.Wounded, 3.4, (0, Sfx.Ending)),
            new Shot(ShotKind.Lifted, 3.4, (0.6, Sfx.Oru)),
            new Shot(ShotKind.Wings, 3.4, (0.3, Sfx.Wings), (1.8, Sfx.Wings)),
            new Shot(ShotKind.Flight, 6.5, (0.4, Sfx.Wings), (3.4, Sfx.Wings)),
            new Shot(ShotKind.TheEnd, 5.0),
        ],
        _ => [],
    };

    public static double Length(SceneId id) => Shots(id).Sum(s => s.Seconds);

    /// <summary>The shot playing so far into a scene, and how far into it.</summary>
    public static (Shot Shot, double T, int Index) At(SceneId id, double t)
    {
        var shots = Shots(id);
        for (int i = 0; i < shots.Count; i++)
        {
            if (t < shots[i].Seconds || i == shots.Count - 1) return (shots[i], Math.Min(t, shots[i].Seconds), i);
            t -= shots[i].Seconds;
        }
        throw new InvalidOperationException("a scene with no shots");
    }
}

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAAnotherWorld.Engine;
using AVAAnotherWorld.Views;
using Controls = AVAAnotherWorld.Engine.Controls;

namespace AVAAnotherWorld.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set AW_SHOTS to a folder to save them as
/// PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open()
    {
        var g = new GameEngine { Silent = true };
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("AW_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAAnotherWorld-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>Lets the computer player play until something worth a picture is on screen.</summary>
    private static bool PlayUntil(GameEngine g, GameCanvas c, Func<GameEngine, bool> moment, int maxSeconds = 120)
    {
        for (int t = 0; t < 60 * maxSeconds && g.State == GameState.Playing; t++)
        {
            if (g.Phase == Phase.Play && moment(g)) { c.Advance(0.016); return true; }
            g.Tick(g.Pilot.Decide(g));
            if (t % 3 == 0) c.Advance(1 / 20.0);
        }
        return false;
    }

    private static void Moment(int section, string name, Func<GameEngine, bool> moment)
    {
        var (w, c, g) = Open();
        g.Go(section);
        Assert.True(PlayUntil(g, c, moment), $"never saw the moment for {name}");
        Shot(w, c, name);
    }

    /// <summary>Plays a scene from the start to a moment in it.</summary>
    private static void Scene(GameEngine g, GameCanvas c, Action start, double at)
    {
        start();
        for (int i = 0; i < 60 * 60 && g.Phase != Phase.Scene; i++) g.Tick(g.Pilot.Decide(g));
        Assert.Equal(Phase.Scene, g.Phase);
        while (g.SceneTime < at) { g.Tick(default); c.Advance(1 / 60.0); }
    }

    [AvaloniaFact]
    public void The_title()
    {
        var (w, c, g) = Open();
        g.Run(30);
        c.Advance(2.6);
        Shot(w, c, "title");
    }

    [AvaloniaFact]
    public void The_lab_on_the_night_of_the_storm()
    {
        var (w, c, g) = Open();
        Scene(g, c, () => g.StartGame(), 5.5 + 2.4);
        Shot(w, c, "lab");
    }

    [AvaloniaFact]
    public void Arrival_in_the_pool()
    {
        // A moment's hesitation in the water, and the tentacles are already rising under him.
        var (w, c, g) = Open();
        g.Go(1);
        for (int i = 0; i < 70; i++) { g.Tick(default); c.Advance(1 / 60.0); }
        for (int i = 0; i < 60 * 4 && !(g.World.Hero.Act == Act.Climb && g.World.Hero.T > 0.12); i++) { g.Tick(g.Pilot.Decide(g)); c.Advance(1 / 60.0); }
        Assert.Equal(Act.Climb, g.World.Hero.Act);
        Assert.True(g.World.Tentacles > Sim.TentacleDepth + 60, $"tentacles at {g.World.Tentacles}");
        Shot(w, c, "pool");
    }

    [AvaloniaFact]
    public void The_beast_gives_chase() =>
        Moment(2, "beast", g => g.World.Foes.FirstOrDefault(f => f.Kind == Kind.Beast) is { Act: Act.Run } b && g.Section == 2 && b.Room == g.World.Hero.Room && g.World.Hero.Room == 2 && g.World.Hero.X < 760 && Math.Abs(b.X - g.World.Hero.X) < 760);

    [AvaloniaFact]
    public void A_gunfight_with_shields()
    {
        // Holding the door while the friend works its panel: the guards come from behind.
        var (w, c, g) = Open();
        g.Go(4);
        g.World.Foes.RemoveAt(0);
        var h = g.World.Hero;
        (h.Room, h.X, h.Facing) = (1, 620, -1);
        g.World.Entered = g.World.Clock;
        // The computer raises its shield; then it charges and charges while the guards come on and fire.
        Assert.True(PlayUntil(g, c, g => g.World.Shields.Any(s => s.Side == Side.Hero) && g.World.Hero.Act == Act.Aim, 10));
        bool seen = false;
        for (int i = 0; i < 60 * 8 && !seen; i++)
        {
            g.Tick(new Controls(Fire: true));
            if (i % 3 == 0) c.Advance(1 / 20.0);
            seen = Pilot.Foe(g.World) is { Act: Act.Aim or Act.Recoil } && g.World.Bolts.Any(b => b.Side == Side.Alien && Math.Abs(b.X - g.World.Hero.X) > 160);
        }
        Assert.True(seen, "never saw the gunfight");
        Shot(w, c, "gunfight");
    }

    [AvaloniaFact]
    public void The_alien_city() => Moment(8, "city", g => g.Section == 8 && g.World.Hero.Act == Act.Leap && g.World.Hero.Room == 0 && g.World.Hero.X > 500);

    [AvaloniaFact]
    public void The_ending()
    {
        var (w, c, g) = Open();
        Scene(g, c, () => g.Go(9), 3.4 + 3.4 + 3.4 + 3.6);
        Assert.Equal(SceneId.Ending, g.Scene);
        Shot(w, c, "ending");
    }
}

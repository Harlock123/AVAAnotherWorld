using AVAAnotherWorld.Engine;

namespace AVAAnotherWorld.Tests;

/// <summary>
/// How the computer player gets on, from the start of the game to the end, cut-scenes and all (set AW_PROBE to run it).
/// Each section reports how long it took and every death on the way.
/// </summary>
public sealed class PilotProbe(ITestOutputHelper output)
{
    [Fact]
    public void How_the_computer_player_does()
    {
        if (Environment.GetEnvironmentVariable("AW_PROBE") is null) return;
        var g = new GameEngine { Silent = true };
        g.StartGame();
        int section = 0, deaths = 0;
        double from = 0;
        var causes = new List<string>();
        for (int t = 0; t < 60 * 60 * 20 && g.State == GameState.Playing; t++)
        {
            if (g.Section != section && g.Phase == Phase.Play)
            {
                if (section > 0) output.WriteLine($"{section} {Sections.Get(section).Name}: {(g.PlayTime - from):0.0}s, {deaths} deaths {string.Join("; ", causes)}");
                section = g.Section;
                from = g.PlayTime;
                deaths = 0;
                causes.Clear();
            }
            bool wasAlive = g.World.Hero.Alive;
            g.Tick(g.Pilot.Decide(g));
            if (wasAlive && !g.World.Hero.Alive && g.Phase == Phase.Dying)
            {
                deaths++;
                causes.Add($"{g.World.Cause} in room {g.World.Hero.Room} at {g.World.Hero.X:0},{g.World.Hero.Y:0} [{g.Pilot.Doing}]");
                if (deaths > 6) break;
            }
        }
        output.WriteLine($"{section} {Sections.Get(section).Name}: {(g.PlayTime - from):0.0}s, {deaths} deaths {string.Join("; ", causes)}");
        output.WriteLine($"{g.State}: {g.PlayTime / 60:0.0} minutes of play, {g.Deaths} deaths in all");
    }
}

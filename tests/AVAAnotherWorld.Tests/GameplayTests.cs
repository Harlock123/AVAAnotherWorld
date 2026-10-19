using Avalonia.Input;
using AVAAnotherWorld.Engine;

namespace AVAAnotherWorld.Tests;

public class GameplayTests
{
    /// <summary>A section from its checkpoint.</summary>
    private static World At(int section)
    {
        var w = new World();
        Sim.Start(w, Sections.Get(section));
        return w;
    }

    /// <summary>One flat room, the floor at 600 from wall to wall, the hero at 200 (with the pistol unless not).</summary>
    private static World Flat(Action<SectionDef>? build = null, bool gun = true, double x = 200)
    {
        var def = new SectionDef
        {
            Name = "TEST", Rooms = [new Room { Look = Look.Cells, Floors = [new Ledge(0, 1024, 600)] }], StartX = x, StartY = 600, Gun = gun,
        };
        build?.Invoke(def);
        var w = new World();
        Sim.Start(w, def);
        return w;
    }

    private static void Run(World w, int steps, Controls c = default)
    {
        for (int i = 0; i < steps && w.Hero.Alive; i++) Sim.Step(w, c);
    }

    private static void RunAll(World w, int steps, Controls c = default)
    {
        for (int i = 0; i < steps; i++) Sim.Step(w, c);
    }

    /// <summary>Fire held for so many steps, then let go.</summary>
    private static void Hold(World w, int steps)
    {
        for (int i = 0; i < steps; i++) Sim.Step(w, new Controls(Fire: true));
        Sim.Step(w, default);
    }

    private static void Tap(World w) => Hold(w, 1);

    private static void Place(World w, int room, double x, double y, Act act = Act.Stand, int facing = 1)
    {
        var h = w.Hero;
        (h.Room, h.X, h.Y, h.Act, h.Facing, h.T, h.Top) = (room, x, y, act, facing, 0, y);
        w.Entered = w.Clock;
    }

    private static Body Guard(World w) => w.Foes.First(f => f.Kind is Kind.Guard or Kind.Warden);

    private static World Duel(double skill = 0, bool shielded = false, Kind kind = Kind.Guard) =>
        Flat(d => d.Foes.Add(new FoeDef(kind, 0, 800, 600, -1, Skill: skill, Gap: 0.8, Shielded: shielded)));

    // ───────────────────────────── moving ─────────────────────────────

    [Fact]
    public void Walking_and_running_go_at_their_own_speeds()
    {
        var w = Flat();
        Run(w, 60, new Controls(1));
        Assert.InRange(w.Hero.X, 200 + Sim.WalkSpeed - 6, 200 + Sim.WalkSpeed + 3);
        Assert.Equal(Act.Walk, w.Hero.Act);
        var v = Flat();
        Run(v, 60, new Controls(1, Run: true));
        Assert.InRange(v.Hero.X, 200 + Sim.RunSpeed - 10, 200 + Sim.RunSpeed + 3);
        Assert.Equal(Act.Run, v.Hero.Act);
        Assert.Contains(Sfx.Step, v.Heard);
    }

    [Fact]
    public void Turning_round_takes_a_moment()
    {
        var w = Flat();
        Sim.Step(w, new Controls(-1));
        Assert.Equal(Act.Turn, w.Hero.Act);
        Assert.Equal(-1, w.Hero.Facing);
        Run(w, (int)(Sim.TurnSeconds * 60) - 2, new Controls(-1));
        Assert.Equal(200, w.Hero.X);
        Run(w, 30, new Controls(-1));
        Assert.True(w.Hero.X < 200);
    }

    [Fact]
    public void A_log_stops_a_walk_and_a_running_leap_clears_it()
    {
        static World Log() => Flat(d => d.Rooms[0].Walls.Add(new Wall(400, 566, 600)));
        var w = Log();
        Run(w, 120, new Controls(1));
        Assert.InRange(w.Hero.X, 370, 400 - Sim.Radius + 0.01);
        var v = Log();
        for (int i = 0; i < 60 && v.Hero.X < 300; i++) Sim.Step(v, new Controls(1, Run: true));
        Sim.Step(v, new Controls(1, Up: true, Run: true));
        Assert.Equal(Act.Leap, v.Hero.Act);
        Run(v, 60);
        Assert.True(v.Hero.Alive);
        Assert.True(v.Hero.X > 420);
    }

    [Fact]
    public void Up_standing_still_is_a_jump_straight_up_and_down_again()
    {
        var w = Flat();
        Sim.Step(w, new Controls(Up: true));
        Assert.Equal(Act.Hop, w.Hero.Act);
        double top = 600;
        for (int i = 0; i < 60; i++) { Sim.Step(w, default); top = Math.Min(top, w.Hero.Y); }
        Assert.True(top < 560);
        Assert.Equal(600, w.Hero.Y);
        Assert.Equal(200, w.Hero.X);
        Assert.Contains(Sfx.Land, w.Heard);
    }

    [Fact]
    public void Down_crouches()
    {
        var w = Flat();
        Run(w, 5, new Controls(Down: true));
        Assert.Equal(Act.Crouch, w.Hero.Act);
        Assert.True(w.Hero.Height < 100);
        Run(w, 2);
        Assert.Equal(Act.Stand, w.Hero.Act);
    }

    [Fact]
    public void Only_a_running_leap_clears_the_broken_bridge()
    {
        var w = At(8);
        for (int i = 0; i < 120 && w.Hero.X < 410; i++) Sim.Step(w, new Controls(1, Run: true));
        Sim.Step(w, new Controls(1, Up: true, Run: true));
        Run(w, 90);
        Assert.True(w.Hero.Alive);
        Assert.True(w.Hero.X > Sections.GapTo);

        var v = At(8);
        for (int i = 0; i < 300 && v.Hero.X < 410; i++) Sim.Step(v, new Controls(1));
        Sim.Step(v, new Controls(1, Up: true));
        Run(v, 180);
        Assert.False(v.Hero.Alive);
        Assert.Equal("a fall", v.Cause);
    }

    [Fact]
    public void A_short_drop_is_nothing_but_a_long_fall_kills()
    {
        // Down the shaft by the step: two short drops, into the room below.
        var w = At(5);
        Run(w, 300, new Controls(1));
        Assert.True(w.Hero.Alive);
        Assert.Equal(1, w.Hero.Room);
        // Off the far end of the high ledge: too far down.
        var v = At(5);
        Place(v, 1, 860, 130);
        Run(v, 200, new Controls(1));
        Assert.False(v.Hero.Alive);
        Assert.Equal("a fall", v.Cause);
    }

    [Fact]
    public void Off_the_side_of_the_screen_is_the_next_room()
    {
        var w = At(4);
        w.Foes.Clear();
        Place(w, 0, 1000, Sections.CellFloor);
        Run(w, 30, new Controls(1));
        Assert.Equal(1, w.Hero.Room);
        Assert.True(w.Hero.X < 100);
        Assert.True(w.Entered > 0);
    }

    // ───────────────────────────── the pistol ─────────────────────────────

    [Fact]
    public void Without_the_pistol_fire_kicks_and_a_kick_squashes_a_leech()
    {
        static World Leech() => Flat(d => d.Foes.Add(new FoeDef(Kind.Leech, 0, 270, 600, -1)), gun: false);
        var w = Leech();
        Run(w, 4);
        Sim.Step(w, new Controls(Fire: true));
        Assert.Equal(Act.Kick, w.Hero.Act);
        Run(w, 30);
        Assert.False(w.Foes[0].Alive);
        Assert.True(w.Hero.Alive);
        Assert.Contains(Sfx.Squish, w.Heard);
        // Left alone, it crawls up and bites.
        var v = Leech();
        Run(v, 60 * 4);
        Assert.False(v.Hero.Alive);
        Assert.Equal("a leech", v.Cause);
    }

    [Fact]
    public void A_tap_of_fire_shoots()
    {
        var w = Flat();
        Tap(w);
        Assert.Single(w.Bolts);
        Assert.False(w.Bolts[0].Super);
        Assert.Equal(Act.Recoil, w.Hero.Act);
        Assert.Empty(w.Shields);
        Assert.Contains(Sfx.Shot, w.Heard);
    }

    [Fact]
    public void Holding_fire_makes_a_shield_wall_in_front()
    {
        var w = Flat();
        for (int i = 0; i < 30; i++) Sim.Step(w, new Controls(Fire: true));
        Assert.Equal(Act.Charge, w.Hero.Act);
        Hold(w, 6);
        Assert.Empty(w.Bolts);
        var s = Assert.Single(w.Shields);
        Assert.Equal(Side.Hero, s.Side);
        Assert.Equal(200 + Sim.ShieldOffset, s.X);
        Assert.Equal(Act.Cast, w.Hero.Act);
    }

    [Fact]
    public void Holding_fire_longer_makes_a_super_shot()
    {
        var w = Flat();
        Hold(w, (int)(Sim.SuperHold * 60) + 6);
        var b = Assert.Single(w.Bolts);
        Assert.True(b.Super);
        Assert.Empty(w.Shields);
        Assert.Contains(Sfx.Super, w.Heard);
    }

    [Fact]
    public void A_side_keeps_two_shields_at_most_and_they_fade()
    {
        var w = Flat();
        for (int i = 0; i < 3; i++) { Sim.Raise(w, w.Hero); RunAll(w, 2); }
        Assert.Equal(2, w.Shields.Count);
        RunAll(w, (int)(Sim.HeroShieldSeconds * 60) + 5);
        Assert.Empty(w.Shields);
    }

    // ───────────────────────────── shields and guards ─────────────────────────────

    [Fact]
    public void A_guard_shoots_a_hero_standing_in_the_open_but_his_shield_stops_the_shots()
    {
        var w = Duel();
        Run(w, 60 * 5);
        Assert.False(w.Hero.Alive);
        Assert.Equal("a guard's shot", w.Cause);
        Assert.Equal(-1, Guard(w).Facing);

        var v = Duel();
        Hold(v, 30);
        Assert.Single(v.Shields);
        Run(v, 60 * 5);
        Assert.True(v.Hero.Alive);
        Assert.Contains(Sfx.Block, v.Heard);
    }

    [Fact]
    public void A_crouch_ducks_under_a_guard_s_shots()
    {
        var w = Duel();
        Run(w, 60 * 5, new Controls(Down: true));
        Assert.True(w.Hero.Alive);
        Assert.Contains(Sfx.Shot, w.Heard);
        Assert.Equal(Act.Crouch, w.Hero.Act);
    }

    [Fact]
    public void A_guard_s_shield_stops_a_shot_but_a_super_shot_breaks_it_and_kills_him()
    {
        var w = Duel(shielded: true);
        Sim.Raise(w, w.Hero);
        Tap(w);
        Run(w, 40);
        Assert.True(Guard(w).Alive);
        Assert.Contains(Sfx.Block, w.Heard);
        Hold(w, (int)(Sim.SuperHold * 60) + 6);
        Run(w, 40);
        Assert.False(Guard(w).Alive);
        Assert.True(w.Hero.Alive);
        Assert.Contains(Sfx.Break, w.Heard);
        Assert.DoesNotContain(w.Shields, s => s.Side == Side.Alien);
    }

    [Fact]
    public void A_quick_guard_raises_a_shield_against_a_shot_coming()
    {
        var w = Duel(skill: 1);
        Sim.Raise(w, w.Hero);
        Run(w, 70);
        Tap(w);
        Run(w, 40);
        Assert.True(Guard(w).Alive);
        Assert.Contains(w.Shields, s => s.Side == Side.Alien);
        // A slow one doesn't.
        var v = Duel(skill: 0);
        Sim.Raise(v, v.Hero);
        Tap(v);
        Run(v, 40);
        Assert.False(Guard(v).Alive);
    }

    [Fact]
    public void Shields_bar_the_other_side_s_way()
    {
        var w = Flat(d => d.Foes.Add(new FoeDef(Kind.Guard, 0, 900, 600, -1, Shielded: true)));
        w.Shields[0].X = 400;
        w.Foes[0].Wake = 999;
        Run(w, 180, new Controls(1));
        Assert.InRange(w.Hero.X, 370, 400 - Sim.Radius + 0.01);
    }

    [Fact]
    public void Guards_walk_in_from_the_side_after_the_hero_comes_in()
    {
        var w = Flat(d => d.Foes.Add(new FoeDef(Kind.Guard, 0, -60, 600, 1, Wake: 1.0, To: 300)), x: 700);
        Run(w, 50);
        Assert.Equal(-60, Guard(w).X);
        Sim.Raise(w, w.Hero);
        w.Hero.Facing = -1;
        w.Shields[0].X = 650;
        w.Shields[0].Life = 99;
        Run(w, 60 * 5);
        Assert.Equal(300, Guard(w).X, 0.5);
        Assert.NotEqual(Act.Walk, Guard(w).Act);
    }

    [Fact]
    public void The_warden_s_super_shot_breaks_shields_and_kills()
    {
        var w = Duel(shielded: true, kind: Kind.Warden);
        Sim.Raise(w, w.Hero);
        Sim.Raise(w, w.Hero);
        w.Shields.ForEach(s => s.Life = 99);
        bool charged = false;
        for (int i = 0; i < 60 * 8 && w.Hero.Alive; i++)
        {
            Sim.Step(w, default);
            charged |= Guard(w).Act == Act.Charge;
        }
        Assert.True(charged);
        Assert.False(w.Hero.Alive);
        Assert.Equal("a super shot", w.Cause);
        Assert.Contains(Sfx.Break, w.Heard);
    }

    // ───────────────────────────── the sections ─────────────────────────────

    [Theory]
    [InlineData(1), InlineData(2), InlineData(3), InlineData(4), InlineData(5), InlineData(6), InlineData(7), InlineData(8), InlineData(9)]
    public void The_computer_player_solves_each_section_from_its_checkpoint(int section)
    {
        var w = At(section);
        var p = new Pilot();
        for (int t = 0; t < 60 * 90 && !w.Done && w.Hero.Alive; t++) Sim.Step(w, p.Decide(w));
        Assert.True(w.Done, $"section {section}: {w.Cause} in room {w.Hero.Room} at {w.Hero.X:0},{w.Hero.Y:0} [{p.Doing}]");
        Assert.True(w.Hero.Alive);
    }

    [Fact]
    public void In_the_pool_the_tentacles_take_whoever_stays_in_the_water()
    {
        var w = At(1);
        Run(w, 60 * 6);
        Assert.False(w.Hero.Alive);
        Assert.Equal("the tentacles", w.Cause);
        // Straight up and out is in time.
        var v = At(1);
        for (int i = 0; i < 60 * 3 && v.Hero.Act is Act.Swim or Act.Climb; i++) Sim.Step(v, new Controls(1, Up: true));
        Run(v, 60 * 4);
        Assert.True(v.Hero.Alive);
        Assert.True(v.Hero.OnGround);
    }

    [Fact]
    public void A_leech_dropping_from_the_roof_gets_a_walker_but_not_a_runner()
    {
        static World Under()
        {
            var w = At(1);
            w.Foes.RemoveAll(f => f.Act == Act.Walk);
            Place(w, 2, 550, Sections.Ground);
            return w;
        }
        var w = Under();
        Run(w, 120, new Controls(1));
        Assert.False(w.Hero.Alive);
        Assert.Equal("a leech", w.Cause);
        var v = Under();
        Run(v, 120, new Controls(1, Run: true));
        Assert.True(v.Hero.Alive);
    }

    [Fact]
    public void The_beast_catches_a_hero_who_only_walks()
    {
        var w = At(2);
        Run(w, 60 * 20, new Controls(-1));
        Assert.False(w.Hero.Alive);
        Assert.Equal("the beast", w.Cause);
    }

    [Fact]
    public void Missing_the_vine_is_a_fall_into_the_tentacles()
    {
        var w = At(2);
        Place(w, 1, 760, 485, facing: -1);
        Run(w, 60 * 3, new Controls(-1));
        Assert.False(w.Hero.Alive);
        Assert.Equal("the tentacles", w.Cause);
    }

    [Fact]
    public void Rocking_with_the_swing_swings_the_cage_higher_and_against_it_lower()
    {
        static double Swing(Func<Cage, int> push)
        {
            var w = At(3);
            w.Cage.Theta = 0.35;
            double most = 0;
            for (int i = 0; i < 60 * 5; i++)
            {
                Sim.Step(w, new Controls(push(w.Cage)));
                if (i > 60 * 2.5) most = Math.Max(most, Math.Abs(w.Cage.Theta));
            }
            return w.Cage.Broken ? 1 : most;
        }
        double free = Swing(_ => 0), with = Swing(c => c.Omega >= 0 ? 1 : -1), against = Swing(c => c.Omega >= 0 ? -1 : 1);
        Assert.True(with > free + 0.05, $"with {with} free {free}");
        Assert.True(against < free - 0.05, $"against {against} free {free}");
    }

    [Fact]
    public void The_cage_crushes_the_guard_if_he_is_under_it_and_if_not_he_shoots()
    {
        static World Breaking()
        {
            var w = At(3);
            w.Cage.Theta = -0.61;
            w.Cage.Omega = -0.4;
            return w;
        }
        var miss = Breaking();
        Guard(miss).X = 800;
        RunAll(miss, 60 * 4);
        Assert.True(miss.Cage.Landed);
        Assert.False(miss.Cage.Crushed);
        Assert.False(miss.Hero.Alive);
        Assert.Equal("a guard's shot", miss.Cause);
        // Where it lands, and when: put the guard there.
        var probe = Breaking();
        Guard(probe).X = 800;
        int steps = 0;
        while (!probe.Cage.Landed) { Sim.Step(probe, default); steps++; }
        var hit = Breaking();
        Guard(hit).X = probe.Cage.X - Sim.PatrolSpeed * steps / 60.0;
        RunAll(hit, 60 * 3);
        Assert.True(hit.Cage.Crushed);
        Assert.False(Guard(hit).Alive);
        Assert.True(hit.Done);
    }

    [Fact]
    public void The_door_stays_shut_until_the_friend_has_worked_its_panel()
    {
        var w = At(4);
        w.Foes.Clear();
        Place(w, 1, 600, Sections.CellFloor);
        Run(w, 120, new Controls(1));
        Assert.True(w.Hero.X < Sections.DoorX);
        Assert.False(w.DoorOpen);
        Run(w, 60 * 12);
        Assert.True(w.DoorOpen);
        Assert.Contains(Sfx.Door, w.Heard);
        Run(w, 120, new Controls(1));
        Assert.True(w.Done);
    }

    [Fact]
    public void Acid_drips_burn_whoever_stands_under_them()
    {
        var w = At(5);
        w.Foes.Clear();
        Place(w, 2, 600, Sections.GrottoFloor);
        Run(w, 60 * 3);
        Assert.False(w.Hero.Alive);
        Assert.Equal("acid", w.Cause);
    }

    [Fact]
    public void Under_the_rock_the_breath_runs_out_and_the_air_pocket_brings_it_back()
    {
        var w = At(6);
        Place(w, 0, 800, 630, Act.Swim);
        Run(w, (int)(Sim.BreathSeconds * 60) + 30);
        Assert.False(w.Hero.Alive);
        Assert.Equal("drowning", w.Cause);
        var v = At(6);
        Place(v, 1, 535, 500, Act.Swim);
        v.Hero.Breath = 2;
        Run(v, 60, new Controls(Up: true));
        Assert.True(v.Hero.Alive);
        Assert.Equal(Sim.BreathSeconds, v.Hero.Breath);
        Assert.Contains(Sfx.Gasp, v.Heard);
    }

    [Fact]
    public void Up_swims_up_and_down_swims_down()
    {
        var w = At(6);
        Place(w, 1, 300, 520, Act.Swim);
        double y = w.Hero.Y;
        Run(w, 20, new Controls(Up: true));
        Assert.True(w.Hero.Y < y - 40);
        y = w.Hero.Y;
        Run(w, 20, new Controls(Down: true));
        Assert.True(w.Hero.Y > y + 40);
    }

    [Fact]
    public void A_barrier_kills_when_it_is_on_and_a_shot_at_the_switch_turns_it_off()
    {
        var w = At(7);
        Place(w, 0, 380, Sections.PlantFloor);
        Run(w, 2);
        Assert.False(w.Hero.Alive);
        Assert.Equal("the barrier", w.Cause);
        var v = At(7);
        v.Foes.Clear();
        Place(v, 1, 450, Sections.PlantFloor);
        Run(v, 60, new Controls(1));
        Assert.False(v.Hero.Alive);
        var s = At(7);
        s.Foes.Clear();
        Place(s, 1, 450, Sections.PlantFloor);
        Tap(s);
        Run(s, 40);
        Assert.True(s.Switch > 0);
        Assert.Contains(Sfx.Switch, s.Heard);
        Run(s, 120, new Controls(1, Run: true));
        Assert.True(s.Hero.Alive);
        Assert.True(s.Done);
    }

    [Fact]
    public void Standing_still_before_the_warden_is_death()
    {
        var w = At(9);
        Run(w, 60 * 10);
        Assert.False(w.Hero.Alive);
    }

    [Fact]
    public void A_copy_of_the_world_goes_its_own_way()
    {
        var w = At(1);
        var c = w.Copy();
        RunAll(c, 60 * 2, new Controls(1, Up: true));
        Assert.Equal(Act.Swim, w.Hero.Act);
        Assert.Equal(Sections.Get(1).StartX, w.Hero.X);
        Assert.Equal(0, w.Clock);
        Assert.NotEqual(c.Hero.X, w.Hero.X);
    }

    // ───────────────────────────── checkpoints and codes ─────────────────────────────

    [Fact]
    public void Every_checkpoint_has_its_own_four_letter_code_and_the_code_brings_it_back()
    {
        var codes = Enumerable.Range(1, Sections.Count).Select(Sections.CodeOf).ToList();
        Assert.Equal(Sections.Count, codes.Distinct().Count());
        for (int n = 1; n <= Sections.Count; n++)
        {
            Assert.Matches("^[A-Z]{4}$", codes[n - 1]);
            Assert.Equal(codes[n - 1], Sections.Get(n).Code);
            Assert.Equal(n, Sections.ByCode(codes[n - 1]));
            Assert.Equal(n, Sections.ByCode(codes[n - 1].ToLowerInvariant()));
        }
        Assert.Equal(0, Sections.ByCode("ABCD"));
        Assert.Equal(0, Sections.ByCode(""));
    }

    [Fact]
    public void A_code_put_in_on_the_title_starts_at_its_checkpoint()
    {
        var g = new GameEngine { Silent = true };
        g.BeginCode();
        Assert.Equal(GameState.Code, g.State);
        foreach (char ch in Sections.CodeOf(6)) g.Entry!.Type(ch);
        g.Run(1);
        Assert.Equal(GameState.Playing, g.State);
        Assert.Equal(6, g.Section);
        Assert.Equal(Phase.Play, g.Phase);
        Assert.Equal(GameEngine.CodeSeconds, g.CodeTime, 0.1);
        Assert.False(g.SubmitCode("ZZZZ"));
        Assert.Equal(GameState.Title, g.State);
        Assert.Equal("NO SUCH CODE", g.Message);
        g.BeginCode();
        g.CancelCode();
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void The_code_entry_types_steps_back_and_dials()
    {
        var e = new CodeEntry();
        e.Type('m');
        e.Type('R');
        e.Back();
        e.Type('Q');
        e.Cycle(-1);
        Assert.Equal("MQZA", e.Code);
        e.Type('Y');
        Assert.False(e.IsDone);
        e.Type('U');
        Assert.True(e.IsDone);
        Assert.Equal("MQYU", e.Code);
        e.Type('!');
        Assert.Equal("MQYU", e.Code);
    }

    [Fact]
    public void A_death_puts_the_hero_back_at_the_checkpoint_at_once()
    {
        var g = new GameEngine { Silent = true };
        g.Go(1);
        Assert.True(g.CodeTime > 0);
        for (int i = 0; i < 60 * 8 && g.Phase != Phase.Dying; i++) g.Tick(default);
        Assert.Equal(Phase.Dying, g.Phase);
        g.Run((int)(GameEngine.DyingSeconds * 60) + 3);
        Assert.Equal(Phase.Play, g.Phase);
        Assert.Equal(1, g.Section);
        Assert.Equal(1, g.Deaths);
        Assert.Equal(Sections.Get(1).StartX, g.World.Hero.X);
        Assert.True(g.World.Hero.Alive);
        Assert.Equal(0, g.CodeTime);
    }

    // ───────────────────────────── the cut-scenes ─────────────────────────────

    [Fact]
    public void A_new_game_opens_with_the_experiment_then_the_pool()
    {
        var g = new GameEngine();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Run(2, default);
        g.Tick(default, start: true);
        Assert.Equal(Phase.Scene, g.Phase);
        Assert.Equal(SceneId.Intro, g.Scene);
        g.Run((int)(SceneBook.Length(SceneId.Intro) * 60) + 5);
        Assert.Contains(Sfx.Rain, heard);
        Assert.Contains(Sfx.Thunder, heard);
        Assert.Contains(Sfx.Surge, heard);
        Assert.Equal(Phase.Play, g.Phase);
        Assert.Equal(1, g.Section);
        Assert.Contains(Sfx.Arrival, heard);
        Assert.Contains(Sfx.Wind, heard);
    }

    [Fact]
    public void A_scene_can_be_skipped_after_a_moment()
    {
        var g = new GameEngine { Silent = true };
        g.StartGame();
        g.Tick(new Controls(Fire: true));
        g.Run(1);
        Assert.Equal(Phase.Scene, g.Phase);
        g.Run((int)(GameEngine.SkipAfter * 60) + 2);
        g.Tick(new Controls(Fire: true));
        Assert.Equal(Phase.Play, g.Phase);
        Assert.Null(g.Scene);
    }

    [Fact]
    public void The_shots_of_a_scene_follow_one_another()
    {
        var shots = SceneBook.Shots(SceneId.Intro);
        Assert.Equal(ShotKind.LabNight, SceneBook.At(SceneId.Intro, 0).Shot.Kind);
        Assert.Equal(ShotKind.Console, SceneBook.At(SceneId.Intro, shots[0].Seconds + 0.1).Shot.Kind);
        Assert.Equal(shots.Count - 1, SceneBook.At(SceneId.Intro, 999).Index);
        foreach (var id in Enum.GetValues<SceneId>()) Assert.True(SceneBook.Length(id) > 4);
    }

    [Theory]
    [InlineData(2, SceneId.Captured)]
    [InlineData(3, SceneId.Freed)]
    [InlineData(8, SceneId.Reunion)]
    public void The_cut_scenes_come_between_the_sections(int section, SceneId scene)
    {
        var g = new GameEngine { Silent = true };
        g.Go(section);
        for (int i = 0; i < 60 * 90 && g.Phase != Phase.Scene; i++) g.Tick(g.Pilot.Decide(g));
        Assert.Equal(scene, g.Scene);
        g.Run((int)(SceneBook.Length(scene) * 60) + 5);
        Assert.Equal(Phase.Play, g.Phase);
        Assert.Equal(section + 1, g.Section);
    }

    [Fact]
    public void After_the_tower_the_ending_then_the_end_then_the_title()
    {
        var g = new GameEngine { Silent = true };
        g.Go(9);
        for (int i = 0; i < 60 * 90 && g.Phase != Phase.Scene; i++) g.Tick(g.Pilot.Decide(g));
        Assert.Equal(SceneId.Ending, g.Scene);
        g.Run((int)(SceneBook.Length(SceneId.Ending) * 60) + 5);
        Assert.Equal(GameState.TheEnd, g.State);
        g.Run((int)(GameEngine.TheEndSeconds * 60) + 5);
        Assert.Equal(GameState.Title, g.State);
    }

    // ───────────────────────────── the game ─────────────────────────────

    [Fact]
    public void The_computer_player_plays_from_the_storm_to_the_end()
    {
        var g = new GameEngine { Silent = true };
        g.StartGame();
        int furthest = 0;
        for (int t = 0; t < 60 * 60 * 10 && g.State == GameState.Playing; t++)
        {
            g.Tick(g.Pilot.Decide(g));
            furthest = Math.Max(furthest, g.Section);
        }
        Assert.Equal(GameState.TheEnd, g.State);
        Assert.Equal(Sections.Count, furthest);
        Assert.Equal(0, g.Deaths);
    }

    [Fact]
    public void Just_holding_right_gets_nowhere()
    {
        var g = new GameEngine { Silent = true };
        g.Go(1);
        g.Run(60 * 30, new Controls(1));
        Assert.Equal(1, g.Section);
        Assert.True(g.Deaths > 0);
    }

    [Fact]
    public void Left_alone_the_title_shows_the_computer_playing_the_opening_then_comes_back()
    {
        var g = new GameEngine { Silent = true };
        g.Run((int)(60 * GameEngine.TitleIdleSeconds) + 5);
        Assert.Equal(GameState.Attract, g.State);
        int furthest = 0;
        for (int i = 0; i < 60 * (GameEngine.AttractSeconds + 5) && g.State == GameState.Attract; i++)
        {
            g.Tick(default);
            furthest = Math.Max(furthest, g.Section);
        }
        Assert.Equal(GameState.Title, g.State);
        Assert.Equal(2, furthest);
    }

    [Fact]
    public void Pause_stops_the_world()
    {
        var g = new GameEngine { Silent = true };
        g.Go(4);
        g.Run(30);
        double t = g.World.Clock;
        g.Pause();
        Assert.Equal(GameState.Paused, g.State);
        g.Run(200);
        Assert.Equal(t, g.World.Clock);
        g.Resume();
        g.Run(10);
        Assert.True(g.World.Clock > t);
    }

    [Fact]
    public void The_keys_reach_the_controls_and_the_controls_the_hero()
    {
        var input = new InputHandler();
        input.OnKeyDown(Key.Left);
        Assert.Equal(-1, input.Player.Dx);
        input.OnKeyUp(Key.Left);
        input.OnKeyDown(Key.Up);
        Assert.True(input.Player.Up);
        input.OnKeyUp(Key.Up);
        input.OnKeyDown(Key.Down);
        Assert.True(input.Player.Down);
        input.OnKeyUp(Key.Down);
        foreach (var k in new[] { Key.Space, Key.LeftCtrl, Key.RightCtrl })
        {
            input.OnKeyDown(k);
            Assert.True(input.Player.Fire);
            input.OnKeyUp(k);
        }
        input.OnKeyDown(Key.LeftShift);
        Assert.True(input.Player.Run);
        input.OnKeyUp(Key.LeftShift);
        Assert.Equal(default, input.Player);

        // Through the handler into the world: right and Shift run; Space shoots.
        var w = Flat();
        input.OnKeyDown(Key.Right);
        input.OnKeyDown(Key.RightShift);
        Run(w, 30, input.Player);
        Assert.Equal(Act.Run, w.Hero.Act);
        Assert.True(w.Hero.X > 300);
        input.Clear();
        input.OnKeyDown(Key.Space);
        Sim.Step(w, input.Player);
        input.OnKeyUp(Key.Space);
        Sim.Step(w, input.Player);
        Assert.Single(w.Bolts);

        // In the water, the Up arrow is up the screen and Down is down it.
        var v = At(6);
        Place(v, 1, 300, 520, Act.Swim);
        input.OnKeyDown(Key.Up);
        Run(v, 20, input.Player);
        Assert.True(v.Hero.Y < 480);
        input.OnKeyUp(Key.Up);
        input.OnKeyDown(Key.Down);
        Run(v, 40, input.Player);
        Assert.True(v.Hero.Y > 520);
        input.Dispose();
    }

    [Fact]
    public void Every_sound_the_game_asks_for_has_its_file()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Sounds");
        foreach (var s in Enum.GetValues<Sfx>())
        {
            if (s is Sfx.Hush or Sfx.Quiet) continue;
            Assert.True(File.Exists(Path.Combine(dir, s + ".wav")), $"no {s}.wav");
        }
    }

    [Fact]
    public void Music_stings_and_ambience_each_go_to_their_channel()
    {
        using var sound = new SoundManager(silent: true);
        sound.Play(Sfx.TitleTheme);
        Assert.EndsWith("TitleTheme.wav", sound.Music);
        sound.Play(Sfx.Quiet);
        Assert.Null(sound.Music);
        sound.Play(Sfx.Captured);
        Assert.EndsWith("Captured.wav", sound.Music);
        sound.Play(Sfx.Wind);
        Assert.EndsWith("Wind.wav", sound.Ambience);
        sound.Play(Sfx.Hush);
        Assert.Null(sound.Ambience);
        sound.Play(Sfx.Rain);
        sound.IsMuted = true;
        Assert.Null(sound.Ambience);
        Assert.Null(sound.Music);
        sound.Play(Sfx.TitleTheme);
        Assert.Null(sound.Music);
    }
}

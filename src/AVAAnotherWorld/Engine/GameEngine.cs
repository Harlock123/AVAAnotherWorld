namespace AVAAnotherWorld.Engine;

public enum GameState { Title, Code, Attract, Playing, Paused, TheEnd }

public enum Phase
{
    /// <summary>A cut-scene playing (fire or Enter skips it).</summary>
    Scene,
    Play,
    /// <summary>Killed: a moment to see how, then the checkpoint again.</summary>
    Dying,
    /// <summary>The section's goal met: on to the next (by way of a cut-scene, perhaps).</summary>
    Done,
}

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Step, Jump, Land, Splash, Gasp, Climb, Grab, Kick, Squish, Bite, Plop, Tentacle, Roar, Thud,
    Shot, Charge, Shield, Block, Super, Break, Zap, Cry, Shout, Oru, Die,
    Creak, Snap, Crash, Door, Work, Switch, Buzz, Drip, Fizz,
    Thunder, Surge, Typing, Wings,
    // Ambience, looping under everything; Hush stops it.
    Rain, Wind, Cave, Machine, Hall, Hush,
    // Music: the title theme, and the stings; Quiet stops it.
    TitleTheme, Arrival, Captured, Freed, Reunion, Ending, Quiet,
}

/// <summary>
/// The whole game with no UI. Call <see cref="Update"/> with the elapsed time and the controls; it runs in fixed 60 Hz
/// steps, so it plays the same at any frame rate. Nine sections, each a checkpoint with a code; a death puts the hero
/// back at the checkpoint at once. Cut-scenes come between some of them.
/// </summary>
public sealed class GameEngine
{
    public const double TitleIdleSeconds = 20, AttractSeconds = 80, DyingSeconds = 1.8, DoneSeconds = 0.5;
    public const double CodeSeconds = 4, TheEndSeconds = 14, SkipAfter = 0.6, MessageSeconds = 2.5;

    private double _accumulator, _idle;
    private bool _startPrev, _firePrev, _tunePending = true;
    private Sfx? _ambience;
    private readonly HashSet<int> _shown = [];

    public GameEngine()
    {
        Load(1);
        State = GameState.Title;
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public Phase Phase { get; private set; } = Phase.Play;
    public double StateTime { get; private set; }
    public double PhaseTime { get; private set; }
    public bool Silent { get; set; }
    public World World { get; private set; } = new();
    public Pilot Pilot { get; } = new();
    public int Section { get; private set; } = 1;
    public SceneId? Scene { get; private set; }
    public double SceneTime { get; private set; }
    public CodeEntry? Entry { get; private set; }
    public string? Message { get; private set; }
    public double MessageTime { get; private set; }
    /// <summary>Seconds more to show the checkpoint's code (when a section is first reached).</summary>
    public double CodeTime { get; private set; }
    public int Deaths { get; private set; }
    /// <summary>Seconds of play (the cut-scenes not counted).</summary>
    public double PlayTime { get; private set; }
    public bool Demo => State == GameState.Attract;
    public SectionDef Def => World.Def;

    private void Play(Sfx s)
    {
        if (Silent) return;
        Sound?.Invoke(s);
    }

    public void Update(double dt, Controls c, bool start)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Field.Step)
        {
            _accumulator -= Field.Step;
            Tick(c, start);
        }
    }

    public void Tick(Controls c, bool start = false)
    {
        double dt = Field.Step;
        StateTime += dt;
        bool startPressed = start && !_startPrev, firePressed = c.Fire && !_firePrev;
        _startPrev = start;
        _firePrev = c.Fire;
        if (MessageTime > 0 && (MessageTime -= dt) <= 0) Message = null;
        switch (State)
        {
            case GameState.Title:
                if (_tunePending) { _tunePending = false; Play(Sfx.TitleTheme); }
                _idle += dt;
                if (startPressed) StartGame();
                else if (_idle > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Code:
                if (Entry is { IsDone: true } entry) SubmitCode(entry.Code);
                break;
            case GameState.Attract:
                if (startPressed) { StartGame(); break; }
                Simulate(Pilot.Decide(this), false, false);
                if (State == GameState.Attract && StateTime > AttractSeconds) ReturnToTitle();
                break;
            case GameState.Playing:
                Simulate(c, startPressed, firePressed);
                break;
            case GameState.TheEnd:
                if (startPressed && StateTime > 2 || StateTime > TheEndSeconds) ReturnToTitle();
                break;
        }
    }

    /// <summary>A new game: the experiment's cut-scene, then the pool. From a code, straight to its checkpoint.</summary>
    public void StartGame(int section = 1, bool intro = true)
    {
        Deaths = 0;
        PlayTime = 0;
        _shown.Clear();
        State = GameState.Playing;
        StateTime = 0;
        Message = null;
        Play(Sfx.Quiet);
        var before = intro ? Sections.Get(section).Before : null;
        Load(section, announce: before == null);
        if (before is { } scene) PlayScene(scene);
    }

    private void StartAttract()
    {
        _shown.Clear();
        _shown.Add(1);
        _shown.Add(2);
        Play(Sfx.Quiet);
        Load(1);
        State = GameState.Attract;
        StateTime = 0;
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        _idle = 0;
        _tunePending = true;
        Scene = null;
        Entry = null;
        Ambience(null);
        Load(1);
    }

    /// <summary>On the title: put in a code.</summary>
    public void BeginCode()
    {
        if (State != GameState.Title) return;
        State = GameState.Code;
        StateTime = 0;
        Entry = new CodeEntry();
    }

    public void CancelCode()
    {
        if (State != GameState.Code) return;
        Entry = null;
        State = GameState.Title;
        StateTime = 0;
        _idle = 0;
    }

    /// <summary>A code put in: its checkpoint, or back to the title if it's no code.</summary>
    public bool SubmitCode(string code)
    {
        Entry = null;
        int n = Sections.ByCode(code);
        if (n == 0)
        {
            State = GameState.Title;
            StateTime = 0;
            _idle = 0;
            Message = "NO SUCH CODE";
            MessageTime = MessageSeconds;
            return false;
        }
        StartGame(n, intro: false);
        return true;
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        State = GameState.Paused;
        Play(Sfx.Hush);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        State = GameState.Playing;
        if (_ambience is { } a) Play(a);
    }

    /// <summary>A section from its checkpoint, as at first or after a death. The code shows the first time.</summary>
    private void Load(int n, bool announce = true)
    {
        Section = n;
        World = new World();
        var def = Sections.Get(n);
        Sim.Start(World, def);
        Phase = Phase.Play;
        PhaseTime = 0;
        Scene = null;
        Pilot.Reset();
        CodeTime = 0;
        if (!announce || State is not (GameState.Playing or GameState.Attract)) return;
        if (_shown.Add(n))
        {
            CodeTime = CodeSeconds;
            if (n == 1) Play(Sfx.Arrival);
        }
        Ambience(def.Ambience);
    }

    private void Ambience(Sfx? a)
    {
        if (a == _ambience) return;
        _ambience = a;
        Play(a ?? Sfx.Hush);
    }

    private void PlayScene(SceneId id)
    {
        Scene = id;
        SceneTime = 0;
        Phase = Phase.Scene;
        PhaseTime = 0;
        Ambience(null);
        Cues(-1, 0);
    }

    /// <summary>The sounds a scene asks for between two moments of it.</summary>
    private void Cues(double from, double to)
    {
        double start = 0;
        foreach (var shot in SceneBook.Shots(Scene!.Value))
        {
            foreach (var (at, sound) in shot.Cues)
                if (start + at > from && start + at <= to)
                {
                    if (sound is Sfx.Rain or Sfx.Wind or Sfx.Cave or Sfx.Machine or Sfx.Hall) Ambience(sound);
                    else if (sound == Sfx.Hush) Ambience(null);
                    else Play(sound);
                }
            start += shot.Seconds;
        }
    }

    private void Simulate(Controls c, bool startPressed, bool firePressed)
    {
        double dt = Field.Step;
        PhaseTime += dt;
        switch (Phase)
        {
            case Phase.Scene:
            {
                double was = SceneTime;
                SceneTime += dt;
                Cues(was, SceneTime);
                if ((startPressed || firePressed) && SceneTime > SkipAfter || SceneTime >= SceneBook.Length(Scene!.Value)) EndScene();
                return;
            }
            case Phase.Play:
                PlayTime += dt;
                if (CodeTime > 0) CodeTime -= dt;
                Sim.Step(World, c);
                if (World.Done) { Phase = Phase.Done; PhaseTime = 0; }
                else if (!World.Hero.Alive) { Phase = Phase.Dying; PhaseTime = 0; Deaths++; }
                break;
            case Phase.Dying:
                // The world goes on a moment (the tentacles drag him down, the beast stands over him).
                Sim.Step(World, default);
                if (PhaseTime > DyingSeconds)
                {
                    if (Demo) { ReturnToTitle(); return; }
                    Load(Section);
                    return;
                }
                break;
            case Phase.Done:
                Sim.Step(World, default);
                if (PhaseTime > DoneSeconds) { Next(); return; }
                break;
        }
        foreach (var s in World.Heard) Play(s);
        World.Heard.Clear();
    }

    /// <summary>A section done: its cut-scene, if it has one (not in the demonstration), then the next.</summary>
    private void Next()
    {
        if (Demo)
        {
            if (Section >= 2) ReturnToTitle();
            else Load(Section + 1);
            return;
        }
        if (Sections.Get(Section).After is { } scene) PlayScene(scene);
        else Advance();
    }

    private void Advance()
    {
        if (Section >= Sections.Count)
        {
            State = GameState.TheEnd;
            StateTime = 0;
            Ambience(null);
            return;
        }
        Load(Section + 1);
    }

    private void EndScene()
    {
        var id = Scene!.Value;
        Scene = null;
        if (id == SceneId.Intro)
        {
            // The pool, from where the scene left it.
            Phase = Phase.Play;
            PhaseTime = 0;
            Play(Sfx.Quiet);
            Load(Section);
            return;
        }
        if (id == SceneId.Ending) { Section = Sections.Count; Advance(); return; }
        Advance();
    }

    // ───────────────────────────── for tests ─────────────────────────────

    public void Run(int steps, Controls c = default)
    {
        for (int i = 0; i < steps; i++) Tick(c);
    }

    /// <summary>Straight into a section, past any cut-scene before it.</summary>
    public void Go(int section = 1) => StartGame(section, intro: false);
}

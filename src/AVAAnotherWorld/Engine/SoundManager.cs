using AVAAnotherWorld.Audio;

namespace AVAAnotherWorld.Engine;

/// <summary>
/// Plays the game's sounds through OpenAL Soft, which mixes as many at once as the game needs. The rain, the wind and
/// the hum of the halls loop on the ambience channel; the title theme and the stings play on the music channel. In a
/// virtual machine a larger mixing buffer keeps the sound from breaking up.
/// </summary>
public sealed class SoundManager : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly string _folder;

    public SoundManager(bool silent = false)
    {
        _folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        _engine = silent
            ? new NullAudioEngine("silent")
            : OpenAlAudioEngine.CreateOrSilent(AudioBuffer.PeriodFrames(AudioBuffer.Automatic, VirtualMachine.Detect()));
        _engine.SetVolumes(0.9f, 0.8f, 0.7f);
        _engine.SetAmbienceVolume(0.5f);
    }

    public bool IsAvailable => _engine.IsAvailable;

    public bool IsMuted
    {
        get => _muted;
        set
        {
            _muted = value;
            if (value) StopAll();
        }
    }
    private bool _muted;

    /// <summary>The ambience loop now playing (for tests).</summary>
    public string? Ambience => _engine.CurrentAmbience;
    /// <summary>The music now playing (for tests).</summary>
    public string? Music => _engine.CurrentMusic;

    private string File(Sfx s) => Path.Combine(_folder, s + ".wav");

    public void Play(Sfx sfx)
    {
        if (_muted) return;
        switch (sfx)
        {
            case Sfx.TitleTheme: _engine.PlayMusic(File(sfx), loop: true); return;
            case Sfx.Arrival or Sfx.Captured or Sfx.Freed or Sfx.Reunion or Sfx.Ending: _engine.PlayMusic(File(sfx), loop: false); return;
            case Sfx.Quiet: _engine.StopMusic(); return;
            case Sfx.Rain or Sfx.Wind or Sfx.Cave or Sfx.Machine or Sfx.Hall: _engine.PlayAmbience(File(sfx)); return;
            case Sfx.Hush: _engine.StopAmbience(); return;
        }
        _engine.PlayEffect(File(sfx), sfx switch
        {
            Sfx.Step => 0.35f, Sfx.Jump or Sfx.Land => 0.45f, Sfx.Drip or Sfx.Creak => 0.5f, Sfx.Typing => 0.6f, Sfx.Charge => 0.7f,
            _ => 1f,
        });
    }

    public void StopAll()
    {
        _engine.StopAmbience();
        _engine.StopMusic();
    }

    public void Dispose() => _engine.Dispose();
}

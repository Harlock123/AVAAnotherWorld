using Avalonia.Input;

namespace AVAAnotherWorld.Engine;

/// <summary>
/// Keyboard and gamepad boiled down to the controls: left and right, up (jump; swim up), down (crouch; swim down),
/// fire and run.
/// </summary>
public sealed class InputHandler : IDisposable
{
    private readonly List<Key> _held = [];
    private readonly Dictionary<PhysicalKey, Key> _pressedAs = [];
    private readonly ControllerManager _pad = new(0);

    public void OnKeyDown(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        if (physical != PhysicalKey.None)
        {
            if (_pressedAs.TryGetValue(physical, out var was) && was != key) _held.Remove(was);
            _pressedAs[physical] = key;
        }
        _held.Remove(key);
        _held.Add(key);
    }

    /// <summary>A key comes up. The key it went down as is let go too: with the keyboard option
    /// shift:both_capslock_cancel, letting go of a Shift arrives as CapsLock, and the Shift would stay held.</summary>
    public void OnKeyUp(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        _held.Remove(key);
        if (physical != PhysicalKey.None && _pressedAs.Remove(physical, out var was)) _held.Remove(was);
    }

    public bool Has(Key key) => _held.Contains(key);
    private bool Any(params Key[] keys) => keys.Any(_held.Contains);

    /// <summary>Lets go of everything (when the window loses the keyboard).</summary>
    public void Clear()
    {
        _held.Clear();
        _pressedAs.Clear();
    }

    public void Update() => _pad.Update();

    /// <summary>
    /// The arrows (or WASD) to walk, Up to jump and Down to crouch; in the water they swim that way (up is up). Shift to
    /// run. Space or Ctrl to kick, or with the pistol: tap to shoot, hold to make a shield, hold longer for a super shot.
    /// On a gamepad: the stick or D-pad, A or B to fire, X or a shoulder button to run.
    /// </summary>
    public Controls Player
    {
        get
        {
            var pad = _pad;
            int dx = (Any(Key.Right, Key.D, Key.NumPad6) ? 1 : 0) - (Any(Key.Left, Key.A, Key.NumPad4) ? 1 : 0);
            if (dx == 0) dx = pad.DPadLeft || pad.LeftStickX < -0.4f ? -1 : pad.DPadRight || pad.LeftStickX > 0.4f ? 1 : 0;
            bool up = Any(Key.Up, Key.W, Key.NumPad8) || pad.DPadUp || pad.LeftStickY < -0.5f;
            bool down = Any(Key.Down, Key.S, Key.NumPad2) || pad.DPadDown || pad.LeftStickY > 0.5f;
            bool fire = Any(Key.Space, Key.LeftCtrl, Key.RightCtrl) || pad.ButtonA || pad.ButtonB;
            bool run = Any(Key.LeftShift, Key.RightShift) || pad.ButtonX || pad.LeftBumper || pad.RightBumper || pad.RightTrigger > 0.5f;
            return new Controls(dx, up, down, fire, run);
        }
    }

    /// <summary>Enter or 1.</summary>
    public bool Start => Any(Key.Enter, Key.D1, Key.NumPad1) || _pad.ButtonStart;

    public bool PadUp => _pad.DPadUp || _pad.LeftStickY < -0.5f;
    public bool PadDown => _pad.DPadDown || _pad.LeftStickY > 0.5f;
    public bool PadLeft => _pad.DPadLeft || _pad.LeftStickX < -0.5f;
    public bool PadRight => _pad.DPadRight || _pad.LeftStickX > 0.5f;
    public bool PadAccept => _pad.ButtonA || _pad.ButtonB;
    /// <summary>Y: put in a code (on the title).</summary>
    public bool PadCode => _pad.ButtonY;
    public bool PadPause => _pad.ButtonStart;
    public bool PadBack => _pad.ButtonBack;

    public void Dispose() => _pad.Dispose();
}

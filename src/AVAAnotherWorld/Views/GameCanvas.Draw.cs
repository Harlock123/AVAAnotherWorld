using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAAnotherWorld.Engine;

namespace AVAAnotherWorld.Views;

/// <summary>
/// Everything is drawn as flat-shaded polygons in a few colours a scene, the way the 1991 game drew: the rooms, the
/// people (skeletons whose limbs are polygons, posed between keyframes), the cut-scenes, even the moons.
/// </summary>
public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAAnotherWorld/Assets/Fonts#VT323"));
    private static readonly Typeface Heavy = new(new FontFamily("fonts:Inter#Inter"), FontStyle.Normal, FontWeight.Black);

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private const uint White = 0xFFF2F0E8, Pale = 0xFFD8D0C0, Dim = 0xFF8A86A0, Amber = 0xFFFFC870, Red = 0xFFE04838;

    private static void Text(DrawingContext dc, string text, Point at, double size, uint colour, bool center = false, bool right = false, Typeface? face = null, uint shadow = 0xC0000000)
    {
        var tf = face ?? Retro;
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, size, Brush(colour));
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        if (shadow != 0) dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, size, Brush(shadow)), new Point(x + 2, at.Y - ft.Height / 2 + 2));
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    /// <summary>A filled polygon from x, y pairs.</summary>
    private static void P(DrawingContext dc, uint colour, params double[] xy)
    {
        if (colour >> 24 == 0) return;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(xy[0], xy[1]), true);
            for (int i = 2; i + 1 < xy.Length; i += 2) c.LineTo(new Point(xy[i], xy[i + 1]));
            c.EndFigure(true);
        }
        dc.DrawGeometry(Brush(colour), null, g);
    }

    private static void Poly(DrawingContext dc, uint colour, IReadOnlyList<Point> pts)
    {
        if (colour >> 24 == 0 || pts.Count < 3) return;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], true);
            for (int i = 1; i < pts.Count; i++) c.LineTo(pts[i]);
            c.EndFigure(true);
        }
        dc.DrawGeometry(Brush(colour), null, g);
    }

    /// <summary>A regular polygon: a moon, a lamp, a joint.</summary>
    private static void Ngon(DrawingContext dc, uint colour, double x, double y, double rx, double ry, int n = 16, double turn = 0)
    {
        var pts = new Point[n];
        for (int i = 0; i < n; i++)
        {
            double a = turn + i * 2 * Math.PI / n;
            pts[i] = new Point(x + Math.Cos(a) * rx, y + Math.Sin(a) * ry);
        }
        Poly(dc, colour, pts);
    }

    private static void Box(DrawingContext dc, uint colour, double x, double y, double w, double h)
    {
        if (colour >> 24 == 0) return;
        dc.FillRectangle(Brush(colour), new Rect(x, y, w, h));
    }

    /// <summary>A thin polygon from one point to another (a ray, a vine, a bar, a bolt's streak).</summary>
    private static void Strip(DrawingContext dc, uint colour, double x0, double y0, double x1, double y1, double w0, double w1)
    {
        double dx = x1 - x0, dy = y1 - y0, len = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy));
        double nx = -dy / len, ny = dx / len;
        P(dc, colour, x0 + nx * w0 / 2, y0 + ny * w0 / 2, x1 + nx * w1 / 2, y1 + ny * w1 / 2, x1 - nx * w1 / 2, y1 - ny * w1 / 2, x0 - nx * w0 / 2, y0 - ny * w0 / 2);
    }

    /// <summary>A sky in flat bands, top colour to bottom.</summary>
    private static void Bands(DrawingContext dc, double y0, double y1, params uint[] colours)
    {
        double h = (y1 - y0) / colours.Length;
        for (int i = 0; i < colours.Length; i++) Box(dc, colours[i], 0, y0 + i * h, Field.Width, h + 1);
    }

    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }

    /// <summary>A number from 0 to 1 that's always the same for the same arguments.</summary>
    private static double Rnd(int i, int salt) => Hash(i, salt, 11) % 10007 / 10007.0;

    private static uint Mix(uint a, uint b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        uint Ch(int shift) => (uint)Math.Round(((a >> shift) & 0xFF) * (1 - t) + ((b >> shift) & 0xFF) * t);
        return Ch(24) << 24 | Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }

    private static uint Alpha(uint colour, double a) => (uint)(Math.Clamp(a, 0, 1) * 255) << 24 | (colour & 0x00FFFFFF);

    private static double Smooth(double k)
    {
        k = Math.Clamp(k, 0, 1);
        return k * k * (3 - 2 * k);
    }

    /// <summary>A jagged skyline of hills or rock from left to right, filled down to <paramref name="bottom"/>.</summary>
    private static void Ridge(DrawingContext dc, uint colour, double baseY, double amp, int seed, double step, double bottom = Field.Height, double x0 = -20, double x1 = Field.Width + 20)
    {
        var pts = new List<Point> { new(x0, bottom) };
        int i = 0;
        for (double x = x0; x <= x1 + step; x += step, i++)
        {
            double h = Math.Sin(x * 0.006 + seed) * 0.5 + Math.Sin(x * 0.017 + seed * 2.1) * 0.3 + (Rnd(i, seed) - 0.5) * 0.4;
            pts.Add(new Point(Math.Min(x, x1), baseY - h * amp));
        }
        pts.Add(new Point(x1, bottom));
        Poly(dc, colour, pts);
    }

    // ───────────────────────────── the screens ─────────────────────────────

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        switch (g.State)
        {
            case GameState.Title: DrawTitle(dc); break;
            case GameState.Code: DrawTitle(dc, behind: true); DrawCode(dc); break;
            case GameState.TheEnd: DrawTheEnd(dc); break;
            default:
                if (g.Phase == Phase.Scene && g.Scene is { } scene) DrawScene(dc, scene, g.SceneTime);
                else DrawWorld(dc, g.World);
                break;
        }
        if (g.State == GameState.Paused)
        {
            Box(dc, 0xC8000000, 0, 0, Field.Width, Field.Height);
            Text(dc, "PAUSED", new Point(512, 300), 84, Amber, center: true);
            Text(dc, $"{g.Def.Name}   CODE {g.Def.Code}", new Point(512, 370), 34, Pale, center: true);
            Text(dc, "ANY KEY TO GO ON  -  ESC TO QUIT", new Point(512, 430), 26, Dim, center: true);
        }
        else if (g.State is GameState.Playing or GameState.Attract && g.Phase != Phase.Scene && g.CodeTime > 0)
        {
            // The checkpoint's code, a moment, in the corner: the only words over the game.
            double a = Math.Min(1, g.CodeTime / 0.6) * Math.Min(1, (GameEngine.CodeSeconds - g.CodeTime) / 0.4);
            Text(dc, $"CODE  {g.Def.Code}", new Point(36, 728), 34, Alpha(Pale, a), shadow: Alpha(0xFF000000, a * 0.75));
        }
        if (g.State == GameState.Attract)
            Text(dc, "DEMONSTRATION  -  PRESS ENTER TO PLAY", new Point(512, 22), 24, Alpha(Amber, 0.85), center: true);
    }

    private void DrawWorld(DrawingContext dc, World w)
    {
        if (w.Section == 3) { DrawHall(dc, w); return; }
        var h = w.Hero;
        var room = w.Here;
        DrawBackdrop(dc, w, room, h.Room);
        DrawRoomThings(dc, w, room, h.Room, front: false);
        foreach (var s in w.Shields)
            if (s.Room == h.Room) DrawShield(dc, w, s, back: true);
        foreach (var f in w.Foes)
            if (f.Room == h.Room) DrawBody(dc, w, f);
        if (w.Friend is { } fr && fr.Room == h.Room) DrawBody(dc, w, fr);
        DrawBody(dc, w, h);
        foreach (var s in w.Shields)
            if (s.Room == h.Room) DrawShield(dc, w, s, back: false);
        foreach (var b in w.Bolts)
            if (b.Room == h.Room) DrawBolt(dc, w, b);
        DrawRoomThings(dc, w, room, h.Room, front: true);
        foreach (var s in w.Sparks)
            if (s.Room == h.Room) DrawSpark(dc, w, s);
        DrawForeground(dc, w, room);
    }

    // ───────────────────────────── title, code and the end ─────────────────────────────

    private void DrawTitle(DrawingContext dc, bool behind = false)
    {
        var g = Engine;
        double cx = Field.Width / 2, t = _clock;
        DrawVista(dc, t, title: true);
        if (!behind)
        {
            DrawTitleWords(dc, cx, 150);
            Text(dc, "AN HOMAGE TO ERIC CHAHI'S 1991 GAME", new Point(cx, 238), 26, Alpha(Pale, 0.9), center: true);
            if ((int)(t * 1.6) % 2 == 0) Text(dc, "PRESS ENTER TO BEGIN", new Point(cx, 540), 44, Amber, center: true);
            Text(dc, "C  -  ENTER A CHECKPOINT CODE", new Point(cx, 590), 28, Pale, center: true);
            if (g.Message is { } msg) Text(dc, msg, new Point(cx, 636), 34, Red, center: true);
            Box(dc, 0xB0000000, 0, 690, Field.Width, 78);
            Text(dc, "ARROWS: WALK   UP: JUMP   DOWN: CROUCH   SHIFT: RUN   SPACE/CTRL: KICK - FIRE (TAP SHOOTS, HOLD SHIELDS, HOLD LONGER: SUPER SHOT)",
                new Point(cx, 710), 18, 0xFFE0D8C8, center: true);
            Text(dc, "P: PAUSE   M: SOUND   CTRL+S: SCREENSHOT        TITLE MUSIC: ERIK SATIE, GYMNOPEDIE NO. 1 (1888)", new Point(cx, 742), 18, Dim, center: true);
        }
    }

    /// <summary>The name, in heavy letters with a deep shadow under them.</summary>
    private static void DrawTitleWords(DrawingContext dc, double cx, double y)
    {
        var small = new FormattedText("AVA", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Heavy, 34, Brush(Amber));
        var big = new FormattedText("ANOTHER WORLD", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Heavy, 104, Brush(White));
        double x = cx - big.Width / 2;
        for (int k = 9; k >= 1; k--)
        {
            var shade = new FormattedText("ANOTHER WORLD", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Heavy, 104, Brush(Mix(0xFF0A0614, 0xFF3A1E3A, 1 - k / 9.0)));
            dc.DrawText(shade, new Point(x + k * 0.8, y - big.Height / 2 + k * 1.4));
        }
        dc.DrawText(big, new Point(x, y - big.Height / 2));
        var lit = new FormattedText("ANOTHER WORLD", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Heavy, 104, Brush(0xFFFFE6B8));
        using (dc.PushClip(new Rect(x - 10, y - big.Height / 2, big.Width + 20, big.Height * 0.48)))
            dc.DrawText(lit, new Point(x, y - big.Height / 2));
        dc.DrawText(small, new Point(x + 6, y - big.Height / 2 - small.Height * 0.55));
    }

    private void DrawCode(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        Box(dc, 0xC0000000, 0, 0, Field.Width, Field.Height);
        Text(dc, "ENTER CODE", new Point(cx, 250), 64, Amber, center: true);
        if (g.Entry is { } entry)
        {
            string s = entry.Code;
            for (int i = 0; i < CodeEntry.Length; i++)
            {
                double x = cx - 165 + i * 110;
                bool cur = i == entry.Cursor;
                P(dc, cur ? 0xFF3A2A50 : 0xFF1E1830, x - 42, 300, x + 42, 300, x + 42, 410, x - 42, 410);
                P(dc, cur ? Amber : 0xFF5A5070, x - 42, 404, x + 42, 404, x + 42, 410, x - 42, 410);
                if (i < entry.Filled || cur && (int)(_clock * 3) % 2 == 0)
                    Text(dc, i < entry.Filled ? s[i].ToString() : "_", new Point(x, 352), 88, cur ? White : Pale, center: true);
            }
        }
        Text(dc, "TYPE THE FOUR LETTERS SHOWN AT A CHECKPOINT", new Point(cx, 470), 28, Pale, center: true);
        Text(dc, "ENTER: GO   BACKSPACE: BACK   ESC: CANCEL", new Point(cx, 510), 24, Dim, center: true);
    }

    private void DrawTheEnd(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        DrawFlight(dc, 6.5, 1);
        Box(dc, 0x90000000, 0, 0, Field.Width, Field.Height);
        Text(dc, "THE END", new Point(cx, 250), 110, White, center: true, face: Heavy);
        int minutes = (int)(g.PlayTime / 60), seconds = (int)(g.PlayTime % 60);
        Text(dc, $"ALL NINE SECTIONS IN {minutes}:{seconds:00}, WITH {g.Deaths} {(g.Deaths == 1 ? "DEATH" : "DEATHS")} ON THE WAY", new Point(cx, 380), 32, Pale, center: true);
        Text(dc, "THEO AND ORU FLY ON, TOWARD THE DAWN", new Point(cx, 430), 28, Dim, center: true);
        if (g.StateTime > 2 && (int)(_clock * 1.6) % 2 == 0) Text(dc, "PRESS ENTER", new Point(cx, 560), 40, Amber, center: true);
    }
}

internal static class DrawingExtensions
{
    public static void FillRectangle(this DrawingContext dc, IBrush brush, Rect rect, double radius) => dc.FillRectangle(brush, rect, (float)radius);
}

using Avalonia;
using Avalonia.Media;
using AVAAnotherWorld.Engine;

namespace AVAAnotherWorld.Views;

/// <summary>
/// The cut-scenes, shot by shot, in the same polygons as the game: the storm and the experiment, the capture, the
/// friend, the flight at the end. Each shot is drawn from how far into it the scene is.
/// </summary>
public sealed partial class GameCanvas
{
    private const double Bar = 56;

    private void DrawScene(DrawingContext dc, SceneId id, double time)
    {
        var (shot, t, index) = SceneBook.At(id, time);
        double len = shot.Seconds;
        switch (shot.Kind)
        {
            case ShotKind.LabNight: DrawLabNight(dc, t); break;
            case ShotKind.Console: DrawConsole(dc, t); break;
            case ShotKind.Accelerator: DrawAccelerator(dc, t, len); break;
            case ShotKind.Strike: DrawStrike(dc, t); break;
            case ShotKind.Surge: DrawLabRoom(dc, t, surge: true); break;
            case ShotKind.EmptyLab: DrawLabRoom(dc, t, surge: false); break;
            case ShotKind.BeastLeap: DrawBeastLeap(dc, t, len, shot: false); break;
            case ShotKind.GuardsFire: DrawBeastLeap(dc, t, len, shot: true); break;
            case ShotKind.Silhouettes: DrawSilhouettes(dc, t, stun: false); break;
            case ShotKind.Stun: DrawSilhouettes(dc, t, stun: true); break;
            case ShotKind.CageCrash: DrawCageCrash(dc, t); break;
            case ShotKind.OruFace: DrawOruFace(dc, t); break;
            case ShotKind.Pistol: DrawPistol(dc, t); break;
            case ShotKind.BridgeWave: DrawBridgeWave(dc, t, pointing: false); break;
            case ShotKind.TowerPoint: DrawBridgeWave(dc, t, pointing: true); break;
            case ShotKind.Wounded: DrawWounded(dc, t, len, lifted: false); break;
            case ShotKind.Lifted: DrawWounded(dc, t, len, lifted: true); break;
            case ShotKind.Wings: DrawWings(dc, t); break;
            case ShotKind.Flight: DrawFlight(dc, t, 0); break;
            case ShotKind.TheEnd: DrawFlight(dc, 6.5 + t, Smooth(t / 1.5)); break;
        }
        // The letterbox, and a dip to black between some shots.
        Box(dc, 0xFF000000, 0, 0, Field.Width, Bar);
        Box(dc, 0xFF000000, 0, Field.Height - Bar, Field.Width, Bar);
        double fade = Math.Max(index == 0 ? 1 - t / 0.5 : 0, shot.Kind is ShotKind.EmptyLab or ShotKind.Stun or ShotKind.TheEnd ? 0 : (t - (len - 0.25)) / 0.25);
        if (shot.Kind is ShotKind.EmptyLab or ShotKind.Stun) fade = Math.Max(fade, (t - (len - 0.8)) / 0.8);
        if (fade > 0) Box(dc, Alpha(0xFF000000, fade), 0, 0, Field.Width, Field.Height);
        if (time > GameEngine.SkipAfter && time < GameEngine.SkipAfter + 3)
            Text(dc, "SPACE OR ENTER SKIPS", new Point(Field.Width - 24, Field.Height - Bar / 2), 20, Alpha(Dim, 0.7), right: true, shadow: 0);
    }

    // ───────────────────────────── the experiment ─────────────────────────────

    private static readonly uint[] Storm = [0xFF070A12, 0xFF0A0E18, 0xFF0E121E, 0xFF121826, 0xFF161C2C, 0xFF1A2232];

    private static double Flash(double t, params double[] at)
    {
        double f = 0;
        foreach (double a in at)
            if (t >= a && t < a + 0.5) f = Math.Max(f, (t - a) < 0.06 ? 1 : (t - a) < 0.12 ? 0.3 : (t - a) < 0.18 ? 0.8 : Math.Max(0, 0.8 - (t - a - 0.18) * 2.5));
        return f;
    }

    private void Rain(DrawingContext dc, double t, uint colour, int count, double speed = 900)
    {
        for (int i = 0; i < count; i++)
        {
            double x = Rnd(i, 300) * 1200 - 100, y = (Rnd(i, 301) * 900 + t * speed) % 900 - 60;
            Strip(dc, colour, x + y * 0.22, y, x + y * 0.22 + 7, y + 30, 1.4, 1.4);
        }
    }

    private void DrawInstitute(DrawingContext dc, double t, double flash, bool dark)
    {
        Ridge(dc, 0xFF0E121A, 470, 30, 301, 70);
        // The accelerator's ring under the ground, its lamps.
        Ngon(dc, 0xFF10141C, 520, 560, 420, 46, 40);
        Ngon(dc, 0xFF0A0E14, 520, 556, 380, 34, 40);
        for (int i = 0; i < 24; i++)
        {
            double a = i * Math.PI / 12, x = 520 + Math.Cos(a) * 400, y = 560 + Math.Sin(a) * 40;
            if (!dark || (int)(t * 9 + i) % 5 == 0) Ngon(dc, Math.Sin(a) > 0 ? 0xFFE0B860 : 0xFF8A7040, x, y, 3, 2, 6);
        }
        // The labs: a long block, a dome, the mast.
        uint block = Mix(0xFF181E2A, 0xFFB8C8E0, flash * 0.6), shade = Mix(0xFF10141E, 0xFF8090A8, flash * 0.6);
        P(dc, block, 300, 520, 300, 400, 720, 400, 720, 520);
        P(dc, shade, 300, 400, 330, 380, 750, 380, 720, 400);
        P(dc, shade, 720, 400, 750, 380, 750, 500, 720, 520);
        var dome = new List<Point>();
        for (int k = 0; k <= 12; k++) dome.Add(new Point(390 - 64 * Math.Cos(k * Math.PI / 12), 382 - 64 * Math.Sin(k * Math.PI / 12)));
        Poly(dc, block, dome);
        P(dc, 0xFF060810, 378, 382, 388, 320, 398, 320, 404, 382);
        P(dc, shade, 640, 380, 648, 140, 656, 140, 664, 380);
        Strip(dc, shade, 652, 160, 610, 200, 2, 2);
        Strip(dc, shade, 652, 160, 694, 200, 2, 2);
        if ((int)(t * 1.5) % 2 == 0) Ngon(dc, 0xFFFF3030, 652, 138, 4, 4, 6);
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 12; c++)
            {
                bool lit = Rnd(c + r * 12, 302) < 0.55 && !(dark && Rnd(c + r * 12, (int)(t * 8)) < 0.85);
                Box(dc, lit ? 0xFFE8C070 : 0xFF0C1018, 316 + c * 34, 412 + r * 26, 20, 12);
            }
        Box(dc, 0xFFFFE0A0, 500, 494, 26, 26);
    }

    private void DrawLabNight(DrawingContext dc, double t)
    {
        double flash = Flash(t, 0.7, 3.6);
        Bands(dc, 0, 470, Storm);
        for (int i = 0; i < 8; i++)
        {
            double x = ((Rnd(i, 310) * 1300 + t * (10 + i * 3)) % 1400) - 200, y = 70 + Rnd(i, 311) * 230;
            Ngon(dc, Mix(i % 2 == 0 ? 0xFF141A28 : 0xFF1A2234, 0xFFC0D0F0, flash * 0.5), x, y, 160 + Rnd(i, 312) * 140, 34 + Rnd(i, 313) * 30, 10, i);
        }
        if (flash > 0.5)
        {
            var pts = new List<Point>();
            double x = 820, y = 60;
            for (int k = 0; k < 9; k++) { pts.Add(new Point(x, y)); x += (Rnd(k, (int)(t > 3 ? 7 : 5)) - 0.5) * 60; y += 40; }
            for (int k = 0; k < pts.Count - 1; k++) Strip(dc, 0xFFF8FAFF, pts[k].X, pts[k].Y, pts[k + 1].X, pts[k + 1].Y, 5 - k * 0.4, 4 - k * 0.4);
        }
        DrawInstitute(dc, t, flash, dark: false);
        // The road, and a car coming up it through the rain.
        P(dc, 0xFF0C0F16, -10, 708, 1034, 640, 1034, 660, -10, 730);
        double cx = Math.Min(560, -120 + t * 200), cy = 700 - (cx + 10) * 0.066;
        P(dc, 0x30FFF0C0, cx + 60, cy - 14, cx + 300, cy - 50, cx + 300, cy + 10, cx + 60, cy - 4);
        P(dc, 0xFF1E2430, cx - 50, cy, cx - 46, cy - 18, cx - 20, cy - 34, cx + 30, cy - 36, cx + 52, cy - 20, cx + 62, cy - 16, cx + 62, cy);
        P(dc, 0xFF3A4C66, cx - 16, cy - 30, cx + 26, cy - 32, cx + 40, cy - 20, cx - 24, cy - 20);
        Ngon(dc, 0xFF08080C, cx - 30, cy, 10, 10, 10);
        Ngon(dc, 0xFF08080C, cx + 40, cy, 10, 10, 10);
        Ngon(dc, 0xFFFFF0C0, cx + 60, cy - 10, 4, 3, 6);
        Rain(dc, t, 0x608090B0, 160);
        if (flash > 0) Box(dc, Alpha(0xFFE8F0FF, flash * 0.55), 0, 0, Field.Width, Field.Height);
        if (t > 1.2) Text(dc, "THE HALVARD INSTITUTE  -  11:47 PM", new Point(60, 690), 30, Alpha(Pale, Math.Min(1, (t - 1.2) * 2)), shadow: 0);
    }

    private void DrawConsole(DrawingContext dc, double t)
    {
        Box(dc, 0xFF0A0E16, 0, 0, Field.Width, Field.Height);
        // The monitor, and its glow on him.
        P(dc, 0x1830C0E0, 580, 0, 1024, 0, 1024, 768, 380, 768);
        P(dc, 0xFF1A2230, 620, 150, 980, 150, 960, 520, 640, 520);
        P(dc, 0xFF06202C, 648, 176, 952, 176, 936, 494, 664, 494);
        for (int r = 1; r <= 4; r++) Ngon(dc, Alpha(0xFF40E0F0, 0.12 + r * 0.05), 800, 335, r * 34, r * 30, 24);
        double a = t * 5;
        for (int k = 0; k < 12; k++)
        {
            double b = a - k * 0.18, rr = 120 - k * 2;
            Ngon(dc, Alpha(0xFFE8FFFF, 1 - k / 12.0), 800 + Math.Cos(b) * rr, 335 + Math.Sin(b) * rr * 0.88, 4 - k * 0.25, 4 - k * 0.25, 6);
        }
        for (int k = 0; k < 6; k++) Box(dc, 0xFF40C0D0, 680, 196 + k * 12, 40 + Rnd(k + (int)(t * 4), 320) * 120, 4);
        Text(dc, $"BEAM {Math.Min(99.9, t * 24):00.0} TEV", new Point(800, 470), 26, 0xFF80F0FF, center: true, shadow: 0);
        // Theo, close: profile, shoulder, the hands at the keys.
        var lit = HeroBuild with { Skin = Mix(HeroBuild.Skin, 0xFF80E0F0, 0.18), SkinDark = Mix(HeroBuild.SkinDark, 0xFF204050, 0.3) };
        P(dc, 0xFFC8D0D8, 120, 768, 150, 560, 260, 480, 420, 470, 500, 520, 540, 768);
        P(dc, 0xFF8A94A2, 120, 768, 150, 560, 230, 500, 260, 768);
        P(dc, lit.SkinDark, 318, 400, 412, 412, 420, 480, 300, 486);
        P(dc, lit.Skin, 300, 476, 400, 470, 404, 520, 330, 540);
        double nod = Math.Sin(t * 1.7) * 0.03;
        DrawHead(dc, lit with { Head = 100 }, new Point(370, 340), 0.1 + nod, p => p, c => c, close: true);
        P(dc, 0xFF1A2030, 520, 640, 980, 600, 1000, 660, 540, 700);
        for (int k = 0; k < 10; k++) P(dc, 0xFF2A3446, 560 + k * 42, 640 - k * 3.5, 596 + k * 42, 637 - k * 3.5, 598 + k * 42, 650 - k * 3.5, 562 + k * 42, 653 - k * 3.5);
        for (int hnd = 0; hnd < 2; hnd++)
        {
            double hx = 640 + hnd * 170, hy = 610 - hnd * 14;
            P(dc, 0xFFC8D0D8, hx - 140, hy + 60, hx - 60, hy + 4, hx - 30, hy + 30, hx - 100, hy + 100);
            P(dc, lit.Skin, hx - 60, hy, hx + 10, hy - 10, hx + 30, hy + 10, hx - 30, hy + 30);
            for (int f = 0; f < 4; f++)
            {
                double lift = Math.Max(0, Math.Sin(t * 14 + f * 1.9 + hnd * 2.5)) * 8;
                P(dc, lit.Skin, hx + f * 10, hy - 4 - lift * 0.3, hx + 8 + f * 10, hy - 6 - lift * 0.3, hx + 14 + f * 10, hy + 18 - lift, hx + 6 + f * 10, hy + 20 - lift);
            }
        }
        P(dc, 0xFF5A3A2A, 930, 540, 972, 540, 968, 596, 934, 596);
        P(dc, 0xFF7A5038, 930, 540, 972, 540, 972, 548, 930, 548);
    }

    private void DrawAccelerator(DrawingContext dc, double t, double len)
    {
        double power = t / len;
        Box(dc, Mix(0xFF06080E, 0xFF203048, power), 0, 0, Field.Width, Field.Height);
        double cx = 512, cy = 384;
        for (int i = 14; i >= 0; i--)
        {
            double z = i + 1 - (t * (2 + power * 10)) % 1, r = 900 / z, a = 0.06 * i;
            if (r < 20) continue;
            uint ring = i % 2 == 0 ? 0xFF2A3446 : 0xFF222A3A;
            Ngon(dc, Mix(ring, 0xFF80A0D0, power * 0.3), cx, cy, r, r, 8, Math.PI / 8 + a * 0);
            Ngon(dc, Mix(0xFF0A0E16, 0xFF304060, power * 0.5), cx, cy, r * 0.82, r * 0.82, 8, Math.PI / 8);
            for (int m = 0; m < 8; m++)
            {
                double ma = m * Math.PI / 4 + Math.PI / 8;
                P(dc, m % 2 == 0 ? 0xFFA03A3A : 0xFF3A5AA0, cx + Math.Cos(ma - 0.12) * r * 0.84, cy + Math.Sin(ma - 0.12) * r * 0.84, cx + Math.Cos(ma + 0.12) * r * 0.84, cy + Math.Sin(ma + 0.12) * r * 0.84,
                    cx + Math.Cos(ma + 0.1) * r * 0.95, cy + Math.Sin(ma + 0.1) * r * 0.95, cx + Math.Cos(ma - 0.1) * r * 0.95, cy + Math.Sin(ma - 0.1) * r * 0.95);
            }
        }
        // The beam: a thread of light growing to a torrent.
        double w = 2 + power * power * 40;
        Ngon(dc, Alpha(0xFF60D0FF, 0.25 + power * 0.4), cx, cy, w * 3, w * 3, 16);
        Ngon(dc, 0xFFE8FFFF, cx, cy, w, w, 16);
        for (int k = 0; k < 10; k++)
        {
            double a = t * 9 + k * 0.63, r = 30 + (t * 300 + k * 47) % 420;
            Strip(dc, Alpha(0xFFC8F4FF, 1 - r / 450), cx + Math.Cos(a) * r, cy + Math.Sin(a) * r, cx + Math.Cos(a) * (r + 40), cy + Math.Sin(a) * (r + 40), 3, 1);
        }
        if (power > 0.85) Box(dc, Alpha(0xFFFFFFFF, (power - 0.85) / 0.15), 0, 0, Field.Width, Field.Height);
    }

    private void DrawStrike(DrawingContext dc, double t)
    {
        double flash = t < 0.5 ? 1 - t * 1.2 : Flash(t, 1.1) * 0.5;
        Bands(dc, 0, 470, Storm);
        DrawInstitute(dc, t, Math.Max(0, flash), dark: t > 0.3);
        if (t < 0.6)
        {
            var pts = new List<Point>();
            double x = 600, y = 0;
            for (int k = 0; k < 8; k++) { pts.Add(new Point(x, y)); x += (652 - x) / (8 - k) + (Rnd(k, 330) - 0.5) * 50; y += 140 / 8.0 + 2; }
            pts.Add(new Point(652, 140));
            for (int k = 0; k < pts.Count - 1; k++) Strip(dc, 0xFFFFFFFF, pts[k].X, pts[k].Y, pts[k + 1].X, pts[k + 1].Y, 9 - k * 0.6, 8 - k * 0.6);
        }
        for (int i = 0; i < 16; i++)
        {
            double a = Rnd(i, 331) * Math.PI * 2, s = (t * 260) * (0.4 + Rnd(i, 332)), x = 652 + Math.Cos(a) * s, y = 140 + Math.Sin(a) * s + t * t * 300;
            if (t < 1.4) Ngon(dc, Alpha(0xFFFFE0A0, 1 - t / 1.4), x, y, 2.5, 2.5, 5);
        }
        Rain(dc, t, 0x608090B0, 160);
        if (flash > 0) Box(dc, Alpha(0xFFF0F4FF, flash * 0.8), 0, 0, Field.Width, Field.Height);
    }

    private static readonly Pose Seated = new(0.16, 0.12, 1.25, 0.65, 1.15, 0.75, 1.5, -1.5, 1.45, -1.5);

    /// <summary>The lab: the physicist at his console as the surge takes him; then the empty chair, turning.</summary>
    private void DrawLabRoom(DrawingContext dc, double t, bool surge)
    {
        Box(dc, 0xFF0C1018, 0, 0, Field.Width, Field.Height);
        // A window on the storm.
        P(dc, 0xFF141C2A, 90, 110, 380, 110, 380, 420, 90, 420);
        using (dc.PushClip(new Rect(90, 110, 290, 310))) Rain(dc, t, 0x507090B0, 40, 700);
        Strip(dc, 0xFF0A0E16, 235, 110, 235, 420, 8, 8);
        Strip(dc, 0xFF0A0E16, 90, 265, 380, 265, 8, 8);
        if (!surge && Flash(t, 1.2) > 0) P(dc, Alpha(0xFFE8F0FF, Flash(t, 1.2)), 90, 110, 380, 110, 380, 420, 90, 420);
        // Racks of equipment.
        for (int i = 0; i < 3; i++)
        {
            double x = 460 + i * 150;
            P(dc, 0xFF1A2230, x, 620, x, 160, x + 120, 160, x + 120, 620);
            for (int k = 0; k < 10; k++)
            {
                bool on = surge || Rnd(k + i * 10, (int)(t * 6)) < 0.3;
                Box(dc, on ? (k % 3 == 0 ? 0xFFE04838 : 0xFF70F0A0) : 0xFF0C1018, x + 14 + (k % 5) * 20, 190 + (k / 5) * 18, 8, 6);
            }
            for (int k = 0; k < 6; k++) Box(dc, 0xFF222C3C, x + 10, 240 + k * 60, 100, 40);
        }
        Box(dc, 0xFF141A24, 0, 620, Field.Width, 148);
        Box(dc, 0xFF2A3446, 0, 620, Field.Width, 5);
        // The console and its screen.
        P(dc, 0xFF1E2636, 560, 620, 600, 470, 860, 470, 900, 620);
        double glare = surge ? Smooth(t / 1.2) : (int)(t * 7) % 3 == 0 ? 0.2 : 0.05;
        P(dc, Mix(0xFF06202C, 0xFFFFFFFF, glare), 640, 330, 820, 330, 830, 460, 630, 460);
        P(dc, 0xFF1A2230, 630, 460, 830, 460, 830, 470, 630, 470);
        // The chair.
        double spin = surge ? 1 : Math.Cos(t * 2.2);
        P(dc, 0xFF2A2A34, 470 - 8, 620, 470 + 8, 620, 470 + 4, 540, 470 - 4, 540);
        P(dc, 0xFF3A3A48, 470 - 44 * spin, 540, 470 + 44 * spin, 540, 470 + 40 * spin, 528, 470 - 40 * spin, 528);
        P(dc, 0xFF3A3A48, 470 - 44 * spin, 528, 470 - 36 * spin, 430, 470 - 24 * spin, 430, 470 - 30 * spin, 528);
        if (surge)
        {
            double gone = Smooth((t - 1.6) / 0.8);
            using (dc.PushTransform(Matrix.CreateTranslation(-470, -540) * Matrix.CreateScale(1.7, 1.7) * Matrix.CreateTranslation(470, 540)))
                DrawPerson(dc, HeroBuild, new Placing(Seated with { ArmN = 1.25 + Math.Sin(t * 6) * 0.2 * (1 - gone) }, 478, 568, Fade: 1 - gone), 1, false, false, 0, false);
            double r = Smooth(t / 2.2) * 700;
            for (int k = 0; k < 4; k++) Ngon(dc, Alpha(k % 2 == 0 ? 0xFFE8FFFF : 0xFF80D8FF, 0.22 + k * 0.08), 700, 400, r * (1 - k * 0.2), r * (1 - k * 0.2), 20, t * (k + 1));
            for (int k = 0; k < 14; k++)
            {
                double a = k * 0.45 + t * 3, rr = r * (0.3 + Rnd(k, 340) * 0.7);
                Strip(dc, 0xC0FFFFFF, 700, 400, 700 + Math.Cos(a) * rr, 400 + Math.Sin(a) * rr, 6, 1);
            }
        }
        else
        {
            for (int k = 0; k < 6; k++)
            {
                double y = 520 - ((t * 40 + k * 30) % 220), x = 470 + Math.Sin(t + k) * 30 + (k - 3) * 8;
                Ngon(dc, Alpha(0xFF8090A0, 0.25 * (y - 300) / 220), x, y, 16 + (520 - y) * 0.15, 10 + (520 - y) * 0.08, 8, k + t);
            }
        }
    }

    // ───────────────────────────── captured ─────────────────────────────

    private void DrawBeastLeap(DrawingContext dc, double t, double len, bool shot)
    {
        Bands(dc, 0, 420, Dusk);
        DrawMoons(dc, 760, 95, 40);
        Ridge(dc, 0xFF7A3456, 340, 46, 41, 60);
        Ridge(dc, 0xFF56264C, 395, 36, 43, 45);
        Box(dc, 0xFF26162C, 0, 560, Field.Width, 208);
        Box(dc, 0xFF6E4258, 0, 560, Field.Width, 5);
        // Theo, down on one knee, the beast in the air above him.
        DrawPerson(dc, HeroBuild, new Placing(Kneeling with { ArmN = 2.4, ArmF = 2.0, Head = -0.2 }, 260, 560), 1, false, false, 0, false);
        if (!shot)
        {
            double k = t / len, x = 900 - k * 420, y = 560 - Math.Sin(Math.PI * Math.Min(1, k * 0.8 + 0.2)) * 200;
            DrawBeastAt(dc, x, y, -1, k * 4, 0.35 - k * 0.5, 1, 0.3, 1.5);
            return;
        }
        double fallK = Smooth((t - 1.0) / 0.6);
        double bx = 470, by = 360 + fallK * 200;
        DrawBeastAt(dc, bx, by, -1, 2, -0.15 + fallK * 1.4, 1 - fallK, 0.2, 1.5);
        foreach (double at in new[] { 0.3, 0.7 })
        {
            double k = (t - at) / 0.2;
            if (k > 0 && k < 1)
            {
                double x = -80 + k * (bx - 40);
                P(dc, 0x80FFC060, x - 70, by - 90, x + 10, by - 94, x + 20, by - 90, x + 10, by - 84, x - 70, by - 86);
                P(dc, 0xFFFFF8E0, x - 50, by - 89, x + 12, by - 91, x + 14, by - 89, x + 12, by - 87, x - 50, by - 88);
            }
            if (t > at + 0.2 && t < at + 0.5) Ngon(dc, Alpha(0xFFFFF0D0, 1 - (t - at - 0.2) / 0.3), bx - 20, by - 90, 40, 40, 12);
        }
    }

    private void DrawSilhouettes(DrawingContext dc, double t, bool stun)
    {
        Bands(dc, 0, Field.Height, Dusk);
        DrawMoons(dc, 300, 160, 60);
        var dark = GuardBuild with { Skin = 0xFF0C0810, SkinDark = 0xFF0C0810, Top = 0xFF0C0810, TopDark = 0xFF0C0810, Legs = 0xFF0C0810, LegsDark = 0xFF0C0810, Feet = 0xFF0C0810, Trim = 0xFF0C0810 };
        var rim = dark with { Skin = 0xFFE07848, SkinDark = 0xFFE07848, Top = 0xFFE07848, TopDark = 0xFFE07848, Legs = 0xFFE07848, LegsDark = 0xFFE07848, Feet = 0xFFE07848, Trim = 0xFFE07848 };
        double[] xs = [250, 520, 790];
        for (int i = 0; i < 3; i++)
        {
            double sway = Math.Sin(t * 1.3 + i) * 0.03;
            var pose = Aiming with { Head = 0.55 + sway, Lean = 0.12, ArmN = stun && i == 1 ? 1.2 + Smooth(t / 0.6) * 0.5 : 0.2, ElbowN = 0.3, ArmF = -0.1 };
            int facing = i == 0 ? 1 : i == 2 ? -1 : (stun ? 1 : -1);
            using (dc.PushTransform(Matrix.CreateTranslation(-xs[i], -Field.Height) * Matrix.CreateScale(3.4, 3.4) * Matrix.CreateTranslation(xs[i], Field.Height + 160)))
            {
                DrawPerson(dc, rim, new Placing(pose, xs[i] + 1.2 * facing, Field.Height - 0.8), facing, i == 1 && stun, false, 0, false);
                DrawPerson(dc, dark, new Placing(pose, xs[i], Field.Height), facing, i == 1 && stun, false, 0, false);
            }
        }
        if (stun)
        {
            double k = Smooth((t - 0.3) / 0.6);
            Ngon(dc, Alpha(0xFFE8FFFF, k), 600, 300, 20 + k * 900, 20 + k * 900, 24);
            Ngon(dc, 0xFFFFFFFF, 600, 300, 10 + k * 600, 10 + k * 600, 24);
        }
    }

    // ───────────────────────────── freed ─────────────────────────────

    private void DrawCageCrash(DrawingContext dc, double t)
    {
        var w = new World();
        Sim.Start(w, Sections.Get(3));
        w.Cage.Broken = w.Cage.Landed = w.Cage.Crushed = true;
        w.Cage.X = 600;
        w.Cage.Y = Sections.HallFloor - Sim.CageHalf;
        w.Foes[0].Act = Act.Dead;
        w.Foes[0].Cause = "the cage";
        w.Foes[0].T = 2;
        w.Foes[0].X = 610;
        w.Clock = t;
        w.Sparks.Add(new Spark(0, 600, Sections.HallFloor, 0, SparkKind.Dust));
        DrawHall(dc, w);
        foreach (var s in w.Sparks) DrawSpark(dc, w, s);
    }

    private void DrawOruFace(DrawingContext dc, double t)
    {
        Box(dc, 0xFF1A1426, 0, 0, Field.Width, Field.Height);
        P(dc, 0xFFC45A4C, 560, 0, 1024, 0, 1024, 768, 700, 768);
        P(dc, 0xFFD87048, 680, 0, 1024, 0, 1024, 500, 760, 520);
        P(dc, 0xFF14101E, 640, 0, 680, 0, 760, 768, 720, 768);
        // His shoulders, his long head, speaking.
        P(dc, OruBuild.SkinDark, 300, 768, 340, 600, 520, 560, 700, 620, 760, 768);
        P(dc, OruBuild.Skin, 420, 768, 440, 610, 560, 580, 680, 640, 700, 768);
        P(dc, OruBuild.Legs, 430, 768, 470, 660, 640, 680, 650, 768);
        double talk = Math.Max(0, Math.Sin(t * 11)) * (t > 0.4 && t < 2.6 ? 1 : 0);
        DrawHead(dc, OruBuild with { Head = 120 }, new Point(560, 330), -0.08 + Math.Sin(t * 0.8) * 0.03, p => new Point(1120 - p.X, p.Y), c => c, close: true, jaw: talk);
    }

    private void DrawPistol(DrawingContext dc, double t)
    {
        Box(dc, 0xFF16121E, 0, 0, Field.Width, Field.Height);
        P(dc, 0x18FFD0A0, 300, 0, 720, 0, 900, 768, 120, 768);
        double meet = Smooth(t / 1.3);
        // Oru's long hand from the right holding the pistol out; Theo's from the left.
        double ox = 1100 - meet * 420, oy = 420;
        P(dc, OruBuild.SkinDark, ox + 60, oy - 40, 1100, oy - 90, 1100, oy + 40, ox + 40, oy + 40);
        P(dc, OruBuild.Skin, ox, oy - 30, ox + 90, oy - 44, ox + 100, oy + 20, ox + 10, oy + 36);
        for (int f = 0; f < 3; f++) P(dc, OruBuild.Skin, ox - 70, oy - 22 + f * 18, ox + 4, oy - 30 + f * 20, ox + 4, oy - 16 + f * 20, ox - 70, oy - 10 + f * 18);
        double gx = ox - 40, gy = oy - 50;
        P(dc, 0xFF1C1C22, gx - 70, gy - 22, gx + 50, gy - 18, gx + 50, gy + 12, gx - 70, gy + 10);
        P(dc, 0xFF5E6676, gx - 60, gy - 18, gx + 40, gy - 15, gx + 40, gy - 6, gx - 60, gy - 8);
        P(dc, 0xFF1C1C22, gx + 10, gy + 10, gx + 40, gy + 10, gx + 30, gy + 60, gx + 2, gy + 56);
        Ngon(dc, 0xFF40C0E0, gx - 66, gy - 6, 6, 6, 8);
        if (t > 1.6 && t < 2.2) Ngon(dc, Alpha(0xFFFFFFFF, 1 - (t - 1.6) / 0.6), gx - 20, gy - 18, 30 * (t - 1.5), 4, 4);
        double tx = -100 + meet * 380, ty = 470;
        P(dc, 0xFFC8D0D8, -10, ty + 30, tx - 40, ty, tx - 30, ty + 60, -10, ty + 90);
        P(dc, HeroBuild.Skin, tx - 40, ty - 4, tx + 40, ty - 10, tx + 60, ty + 30, tx - 30, ty + 56);
        P(dc, HeroBuild.Skin, tx + 30, ty - 8, tx + 80, ty - 30, tx + 88, ty - 18, tx + 46, ty + 6);
    }

    // ───────────────────────────── the friend again ─────────────────────────────

    private void DrawBridgeWave(DrawingContext dc, double t, bool pointing)
    {
        DrawCity(dc, t, pointing ? 1 : 0);
        if (pointing)
        {
            // The tower, on its crag, a light at the top.
            P(dc, 0xFF4A2848, 760, 600, 790, 300, 800, 120, 812, 80, 824, 120, 834, 300, 864, 600);
            Ngon(dc, 0xFF4A2848, 812, 150, 40, 18, 12);
            Ngon(dc, (int)(t * 2) % 2 == 0 ? 0xFFFF4040 : 0xFF802020, 812, 74, 5, 5, 8);
        }
        DrawDeck(dc, new Room { Floors = [new Ledge(0, 1024, Sections.Deck, Sections.Deck, 60)] });
        double wave = Math.Sin(t * 6);
        var oru = pointing ? Standing with { ArmN = 2.35, ElbowN = 0.05, Head = -0.35, Lean = -0.04 } : Standing with { ArmN = 2.9 + wave * 0.35, ElbowN = 0.3 + wave * 0.2 };
        DrawPerson(dc, OruBuild, new Placing(oru, pointing ? 600 : 700, Sections.Deck), pointing ? 1 : -1, false, false, 0, false);
        double hx = pointing ? 520 : Math.Min(540, 80 + t * 150);
        bool walking = !pointing && hx < 540;
        DrawPerson(dc, HeroBuild, new Placing(walking ? Cycle(WalkHalf, t * 150 / 130) : Standing with { Head = pointing ? -0.25 : 0 }, hx, Sections.Deck), 1, false, false, 0, false);
    }

    // ───────────────────────────── the end ─────────────────────────────

    private void DrawWounded(DrawingContext dc, double t, double len, bool lifted)
    {
        DrawTower(dc);
        if (!lifted)
        {
            double k = Smooth(t / (len * 0.8));
            var pose = Pose.Lerp(Standing with { ArmN = 0.6, ElbowN = 1.6, Head = 0.2 }, Seated with { Lean = -0.35, Head = 0.5, ArmN = 0.7, ElbowN = 1.6, ArmF = 0.2, HipN = 1.5, KneeN = -0.15, HipF = 1.35, KneeF = -0.4 }, k);
            DrawPerson(dc, HeroBuild, new Placing(pose, 200 + k * 10, Sections.TowerFloor - k * 2), 1, false, false, 0, false);
            if (t > 1.2) DrawPerson(dc, OruBuild, new Placing(Cycle(WalkHalf, t * 1.1), Math.Max(420, 1060 - (t - 1.2) * 300), Sections.TowerFloor), -1, false, false, 0, false);
            return;
        }
        double up = Smooth(t / (len * 0.7));
        var kneel = Pose.Lerp(Kneeling with { ArmN = 1.3, ElbowN = 0.9, ArmF = 1.2, ElbowF = 0.9 }, Standing with { ArmN = 1.2, ElbowN = 1.0, ArmF = 1.1, ElbowF = 1.0, Head = 0.3 }, up);
        DrawPerson(dc, OruBuild, new Placing(kneel, 330, Sections.TowerFloor), -1, false, false, 0, false);
        double carryY = Sections.TowerFloor - 40 - up * 62;
        DrawPerson(dc, HeroBuild, new Placing(Lying with { ArmN = 0.4 + up * 0.2, HipN = 0.9, KneeN = -1.4, HipF = 0.8, KneeF = -1.2, Head = 0.6 }, 262, carryY, -Math.PI / 2 + 0.1), -1, false, false, 0, false);
        var arms = kneel;
        DrawPerson(dc, OruBuild with { Legs = 0, LegsDark = 0, Feet = 0, Top = 0, TopDark = 0, Trim = 0, SkinDark = 0 }, new Placing(arms, 330, Sections.TowerFloor), -1, false, false, 0, false);
    }

    /// <summary>The winged creature: a long neck and beak, great membrane wings beating.</summary>
    private static void DrawFlyer(DrawingContext dc, double x, double y, double flap, double scale, bool riders)
    {
        double up = Math.Sin(flap);
        Point M(double lx, double ly) => new(x + lx * scale, y + ly * scale);
        void Pg(uint c, params double[] xy)
        {
            var pts = new Point[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = M(xy[i * 2], xy[i * 2 + 1]);
            Poly(dc, c, pts);
        }
        // The far wing, the body, the near wing.
        Pg(0xFF2A1E34, -10, -10, -40, -60 - up * 70, -110, -90 - up * 90, -60, -40 - up * 30, -30, 0);
        Pg(0xFF3A2A44, -70, 0, -40, -16, 30, -18, 70, -10, 110, -30, 140, -40, 156, -36, 130, -22, 90, -4, 40, 8, -30, 10, -90, 6, -130, 16, -100, -2);
        Pg(0xFFE0A060, 152, -38, 186, -32, 156, -30);
        Ngon(dc, 0xFFFFE080, M(140, -36).X, M(140, -36).Y, 2 * scale, 2 * scale, 6);
        Pg(0xFF5A3E66, 0, -14, -10, -80 - up * 90, -60, -160 - up * 120, -20, -110 - up * 80, 30, -60 - up * 40, 40, -10);
        Pg(0xFF2A1E34, 0, -14, -60, -160 - up * 120, -54, -150 - up * 112, 4, -16);
        Pg(0xFF2A1E34, 10, -14, -20, -110 - up * 80, -16, -106 - up * 78, 14, -14);
        Pg(0xFF3A2A44, -130, 16, -170, 26, -160, 14, -128, 8);
        if (riders)
        {
            using (dc.PushTransform(Matrix.CreateTranslation(-x, -y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
            {
                DrawPerson(dc, HeroBuild, new Placing(Seated with { Lean = 0.5, Head = 0.3, ArmN = 0.9, HipN = 1.3, KneeN = -1.0 }, x - 30, y - 10), 1, false, false, 0, false);
                DrawPerson(dc, OruBuild, new Placing(Seated with { Lean = 0.3, ArmN = 1.0, ArmF = 1.2, HipN = 1.3, KneeN = -1.0 }, x + 12, y - 12), 1, false, false, 0, false);
            }
        }
    }

    private void DrawWings(DrawingContext dc, double t)
    {
        DrawTower(dc);
        double land = Smooth(t / 1.6);
        DrawFlyer(dc, 600 + (1 - land) * 500, 320 + (1 - land) * -200 + land * 90, t * (land < 1 ? 9 : 3), 1.25, false);
        DrawPerson(dc, OruBuild, new Placing(Standing with { ArmN = 1.2, ElbowN = 1.0, ArmF = 1.1, ElbowF = 1.0, Head = -0.3 }, 330, Sections.TowerFloor), -1, false, false, 0, false);
        DrawPerson(dc, HeroBuild, new Placing(Lying with { HipN = 0.9, KneeN = -1.4, HipF = 0.8, KneeF = -1.2, Head = 0.6 }, 262, Sections.TowerFloor - 102, -Math.PI / 2 + 0.1), -1, false, false, 0, false);
        DrawPerson(dc, OruBuild with { Legs = 0, LegsDark = 0, Feet = 0, Top = 0, TopDark = 0, Trim = 0, SkinDark = 0 }, new Placing(Standing with { ArmN = 1.2, ElbowN = 1.0, ArmF = 1.1, ElbowF = 1.0, Head = -0.3 }, 330, Sections.TowerFloor), 1, false, false, 0, false);
    }

    /// <summary>Over the city toward the sunrise, the two of them on the creature's back.</summary>
    private void DrawFlight(DrawingContext dc, double t, double end)
    {
        DrawCity(dc, t, 1);
        double x = -200 + t * 190, y = 300 - Math.Sin(t * 0.6) * 40;
        if (end <= 0 || t < 7.5) DrawFlyer(dc, Math.Min(x, 1300), y, t * 6, 1.3, true);
        if (end > 0)
        {
            Box(dc, Alpha(0xFF000000, end * 0.35), 0, 0, Field.Width, Field.Height);
            Text(dc, "THE END", new Point(512, 360), 120, Alpha(White, end), center: true, face: Heavy, shadow: Alpha(0xFF000000, end * 0.7));
        }
    }
}

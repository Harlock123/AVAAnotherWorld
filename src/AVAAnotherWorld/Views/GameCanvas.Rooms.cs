using Avalonia;
using Avalonia.Media;
using AVAAnotherWorld.Engine;

namespace AVAAnotherWorld.Views;

/// <summary>
/// The rooms: a backdrop painted in flat bands and silhouettes for each, the floors as slabs of rock or metal, the
/// water, the vine, the barriers and drips; then the bolts, the shields and the sparks.
/// </summary>
public sealed partial class GameCanvas
{
    /// <summary>The wilds at dusk, top of the sky to the horizon.</summary>
    private static readonly uint[] Dusk =
        [0xFF120A26, 0xFF1A1032, 0xFF24143E, 0xFF321A48, 0xFF44204E, 0xFF5A2652, 0xFF742E54, 0xFF903854, 0xFFAC4650, 0xFFC85A4A, 0xFFDE7848];

    /// <summary>The city's sky, near dawn.</summary>
    private static readonly uint[] Dawn =
        [0xFF1C1430, 0xFF2A1A3E, 0xFF3C2248, 0xFF542A50, 0xFF6E3456, 0xFF8A4058, 0xFFA64E58, 0xFFC26258, 0xFFD87C5C, 0xFFE89C66, 0xFFF2BC7A];

    private void DrawBackdrop(DrawingContext dc, World w, Room room, int index)
    {
        switch (room.Look)
        {
            case Look.Pool: DrawPool(dc, w, room); break;
            case Look.Leeches: DrawLeechRun(dc, room); break;
            case Look.Cliff: DrawVista(dc, _clock, title: false); DrawLedges(dc, room, 0xFF24142A, 0xFF6E4258, 0xFF160C1C, 4); break;
            case Look.Clearing: DrawClearing(dc, room); break;
            case Look.Cells or Look.Gate: DrawCells(dc, w, room); break;
            case Look.ShaftTop or Look.ShaftMid: DrawShaft(dc, room, room.Look == Look.ShaftTop); break;
            case Look.Grotto: DrawGrotto(dc, room); break;
            case Look.Shore or Look.Sunken or Look.Landing: DrawSunken(dc, w, room, index); break;
            case Look.Plant or Look.Switchroom: DrawPlant(dc, room); break;
            case Look.Bridge or Look.Span: DrawCity(dc, _clock, room.Look == Look.Bridge ? 0 : 1); DrawDeck(dc, room); break;
            case Look.Tower: DrawTower(dc); break;
        }
    }

    // ───────────────────────────── rock and floors ─────────────────────────────

    /// <summary>The room's floors as rock: a lit top edge, a body with facets, solid to the bottom or a slab with a ragged underside.</summary>
    private static void DrawLedges(DrawingContext dc, Room room, uint body, uint top, uint dark, int seed)
    {
        int n = 0;
        foreach (var l in room.Floors) DrawLedge(dc, l, body, top, dark, seed * 7 + n++);
    }

    private static void DrawLedge(DrawingContext dc, Ledge l, uint body, uint top, uint dark, int seed)
    {
        double x0 = l.X0 <= 0 ? -10 : l.X0, x1 = l.X1 >= Field.Width ? Field.Width + 10 : l.X1;
        var pts = new List<Point> { new(x0, l.YAt(x0)), new(x1, l.YAt(x1)) };
        if (l.Thick <= 0)
        {
            // Solid ground: a rough cliff face down each open end.
            if (l.X1 < Field.Width) for (int i = 1; i <= 6; i++) pts.Add(new Point(x1 - 6 + Rnd(i, seed) * 14 - i * 1.5, l.Y1 + i * (Field.Height - l.Y1) / 6));
            else pts.Add(new Point(x1, Field.Height));
            if (l.X0 > 0) for (int i = 6; i >= 1; i--) pts.Add(new Point(x0 + 6 - Rnd(i, seed + 3) * 14 + i * 1.5, l.Y0 + i * (Field.Height - l.Y0) / 6));
            else pts.Add(new Point(x0, Field.Height));
        }
        else
        {
            int steps = Math.Max(3, (int)((x1 - x0) / 40));
            for (int i = steps; i >= 0; i--)
            {
                double x = x0 + (x1 - x0) * i / steps, edge = i == 0 || i == steps ? 0.4 : 1;
                pts.Add(new Point(x, l.YAt(x) + l.Thick * (0.6 + 0.6 * Rnd(i, seed)) * edge));
            }
        }
        Poly(dc, body, pts);
        // Facets.
        double bottom = l.Thick <= 0 ? Field.Height : Math.Max(l.Y0, l.Y1) + l.Thick;
        for (int i = 0; i < (int)((x1 - x0) / 55) + 1; i++)
        {
            double fx = x0 + 10 + Rnd(i, seed + 9) * Math.Max(1, x1 - x0 - 40), fy = l.YAt(fx) + 16 + Rnd(i, seed + 5) * Math.Max(1, bottom - l.YAt(fx) - 40);
            double s = 10 + Rnd(i, seed + 2) * 22;
            if (fy + s > bottom - 4) continue;
            P(dc, dark, fx, fy, fx + s * 1.4, fy + s * 0.3, fx + s * 0.9, fy + s, fx - s * 0.3, fy + s * 0.7);
        }
        // The lit edge along the top.
        P(dc, top, x0, l.YAt(x0), x1, l.YAt(x1), x1, l.YAt(x1) + 5, x0, l.YAt(x0) + 5);
        P(dc, Mix(top, body, 0.55), x0, l.YAt(x0) + 5, x1, l.YAt(x1) + 5, x1, l.YAt(x1) + 11, x0, l.YAt(x0) + 11);
    }

    /// <summary>A block of rock under water, its edges broken up.</summary>
    private static void DrawRock(DrawingContext dc, Zone z, uint body, uint edge, int seed)
    {
        var pts = new List<Point>();
        double J(int i, double amp) => (Rnd(i, seed) - 0.5) * amp;
        bool l = z.X0 <= 0, r = z.X1 >= Field.Width, t = z.Y0 <= 0, b = z.Y1 >= Field.Height;
        int nx = Math.Max(2, (int)((z.X1 - z.X0) / 36)), ny = Math.Max(2, (int)((z.Y1 - z.Y0) / 36));
        for (int i = 0; i <= nx; i++) pts.Add(new Point(z.X0 + (z.X1 - z.X0) * i / nx + (i > 0 && i < nx ? J(i, 14) : 0), t ? -10 : z.Y0 + J(i + 40, 14)));
        for (int i = 1; i <= ny; i++) pts.Add(new Point(r ? Field.Width + 10 : z.X1 + J(i + 80, 14), z.Y0 + (z.Y1 - z.Y0) * i / ny));
        for (int i = nx - 1; i >= 0; i--) pts.Add(new Point(z.X0 + (z.X1 - z.X0) * i / nx + (i > 0 ? J(i + 120, 14) : 0), b ? Field.Height + 10 : z.Y1 + J(i + 160, 14)));
        for (int i = ny - 1; i >= 1; i--) pts.Add(new Point(l ? -10 : z.X0 + J(i + 200, 14), z.Y0 + (z.Y1 - z.Y0) * i / ny));
        Poly(dc, body, pts);
        for (int i = 0; i < (nx * ny) / 3 + 1; i++)
        {
            double fx = z.X0 + 8 + Rnd(i, seed + 7) * Math.Max(1, z.X1 - z.X0 - 30), fy = z.Y0 + 8 + Rnd(i, seed + 8) * Math.Max(1, z.Y1 - z.Y0 - 30), s = 8 + Rnd(i, seed + 9) * 18;
            P(dc, edge, fx, fy, fx + s, fy + s * 0.2, fx + s * 0.6, fy + s * 0.8);
        }
    }

    // ───────────────────────────── the wilds ─────────────────────────────

    private void DrawMoons(DrawingContext dc, double x, double y, double r)
    {
        Ngon(dc, 0x20F0E0D0, x, y, r * 1.5, r * 1.5, 24);
        Ngon(dc, 0xFFF0E2C8, x, y, r, r, 28);
        P(dc, 0xFFD2BCA4, x - r * 0.15, y - r * 0.98, x + r * 0.55, y - r * 0.82, x + r * 0.95, y - r * 0.3, x + r * 0.98, y + r * 0.2, x + r * 0.7, y + r * 0.72, x + r * 0.2, y + r * 0.98, x + r * 0.55, y + r * 0.4, x + r * 0.62, y - r * 0.2, x + r * 0.4, y - r * 0.7);
        Ngon(dc, 0xFFDCC8B0, x - r * 0.35, y - r * 0.2, r * 0.18, r * 0.14, 8);
        Ngon(dc, 0xFFDCC8B0, x - r * 0.05, y + r * 0.4, r * 0.12, r * 0.1, 8);
        Ngon(dc, 0xFFD8C8E8, x + r * 2.2, y - r * 0.9, r * 0.26, r * 0.26, 16);
        P(dc, 0xFFA898C0, x + r * 2.2, y - r * 1.16, x + r * 2.46, y - r * 0.9, x + r * 2.2, y - r * 0.64, x + r * 2.32, y - r * 0.9);
    }

    private void DrawPool(DrawingContext dc, World w, Room room)
    {
        Bands(dc, 0, 420, Dusk);
        DrawMoons(dc, 190, 105, 52);
        Ridge(dc, 0xFF7A3456, 350, 40, 3, 60);
        Ridge(dc, 0xFF56264C, 395, 34, 8, 45);
        Ridge(dc, 0xFF381A3C, 440, 22, 13, 34);
        // The tree whose vine hangs over the pool.
        P(dc, 0xFF0E0812, 1024, 0, 1024, 70, 900, 52, 760, 40, 600, 26, 470, 22, 420, 12, 470, 0);
        P(dc, 0xFF0E0812, 1024, 40, 1024, 380, 990, 420, 960, 300, 950, 150, 930, 60);
        for (int i = 0; i < 16; i++)
        {
            // Fronds hanging from the branch.
            double lx = 440 + i * 38 + Rnd(i, 2) * 20, ly = 18 + Rnd(i, 3) * 26, len = 40 + Rnd(i, 4) * 60, lean = (Rnd(i, 5) - 0.5) * 30;
            uint c = i % 3 == 0 ? 0xFF1E1426 : 0xFF140C1A;
            P(dc, c, lx - 9, ly, lx + 9, ly, lx + lean * 0.5 + 6, ly + len * 0.6, lx + lean, ly + len, lx + lean * 0.5 - 6, ly + len * 0.6);
        }
        // The vine.
        if (room.Vine is { } vine)
        {
            double sway = Math.Sin(_clock * 0.9) * 6;
            var h = w.Hero;
            bool held = h.Act == Act.Swing && h.Room == 1;
            double tipX = held ? h.X : vine.X + sway, tipY = held ? h.Y - 124 : vine.Tip;
            for (int s = 0; s < 8; s++)
            {
                double k0 = s / 8.0, k1 = (s + 1) / 8.0;
                double x0 = vine.X + (tipX - vine.X) * k0 + Math.Sin(k0 * 3) * 4, x1 = vine.X + (tipX - vine.X) * k1 + Math.Sin(k1 * 3) * 4;
                Strip(dc, 0xFF1E3A24, x0, 20 + (tipY - 20) * k0, x1, 20 + (tipY - 20) * k1, 6, 5);
                if (s % 2 == 0) P(dc, 0xFF2E5A32, x1, 20 + (tipY - 20) * k1, x1 + 12, 20 + (tipY - 20) * k1 - 4, x1 + 4, 20 + (tipY - 20) * k1 + 8);
            }
            Ngon(dc, 0xFF2E5A32, tipX, tipY + 6, 5, 8, 6);
        }
        // The water in its basin: tentacles at the bottom.
        var water = room.Water!.Value;
        Box(dc, 0xFF0A1A2E, water.X0, water.Y0, water.X1 - water.X0, water.Y1 - water.Y0);
        uint[] deep = [0xFF2A6486, 0xFF225678, 0xFF1C486A, 0xFF163C5C, 0xFF12304E, 0xFF0E2640, 0xFF0A1C32];
        double bh = (water.Y1 - water.Y0) / deep.Length;
        for (int i = 0; i < deep.Length; i++) Box(dc, deep[i], water.X0, water.Y0 + i * bh, water.X1 - water.X0, bh + 1);
        for (int i = 0; i < 4; i++)
        {
            double rx = water.X0 + 40 + i * 100 + Math.Sin(_clock * 0.5 + i) * 10;
            P(dc, 0x143C8CB4, rx, water.Y0, rx + 46, water.Y0, rx + 120, water.Y1, rx + 50, water.Y1);
        }
        DrawTentacles(dc, w, front: false);
        // The banks and the basin's sides.
        DrawLedges(dc, room, 0xFF26162C, 0xFF6E4258, 0xFF180C1E, 1);
        P(dc, 0xFF26162C, 290, Sections.Surface, 312, Sections.Surface + 40, 304, 560, 318, 640, 306, 768, 280, 768);
        P(dc, 0xFF26162C, 736, Sections.Surface, 716, Sections.Surface + 30, 724, 540, 710, 620, 722, 700, 714, 768, 744, 768);
        // Plants on the banks.
        for (int i = 0; i < 5; i++)
        {
            double px = 30 + i * 52 + Rnd(i, 7) * 20, ph = 60 + Rnd(i, 8) * 90;
            Strip(dc, 0xFF140A18, px, Sections.Surface + 2, px + 8, Sections.Surface - ph, 5, 2);
            Ngon(dc, 0xFF1E1024, px + 8, Sections.Surface - ph, 9, 12, 7, i);
            Ngon(dc, 0xFFE07A50, px + 9, Sections.Surface - ph - 2, 2.4, 2.4, 5);
        }
    }

    private void DrawLeechRun(DrawingContext dc, Room room)
    {
        Bands(dc, 0, 470, Dusk);
        Ridge(dc, 0xFF7A3456, 420, 50, 21, 70);
        Ridge(dc, 0xFF4E2248, 470, 40, 23, 50);
        // Canyon walls each side, and the overhang across the top.
        P(dc, 0xFF2C1834, -10, 0, 160, 0, 190, 140, 150, 300, 180, 460, 130, 620, -10, 620);
        P(dc, 0xFF2C1834, 1034, 0, 870, 0, 840, 160, 890, 330, 860, 480, 900, 620, 1034, 620);
        var roof = new List<Point> { new(-10, -10), new(1034, -10) };
        for (int i = 20; i >= 0; i--)
        {
            double x = i * 1024 / 20.0;
            roof.Add(new Point(x, 110 + Math.Sin(i * 1.3) * 18 + Rnd(i, 31) * 26 + (i % 3 == 0 ? 30 : 0)));
        }
        Poly(dc, 0xFF1A0E20, roof);
        for (int i = 0; i < 14; i++)
        {
            double x = 30 + i * 72 + Rnd(i, 32) * 30, y = 120 + Rnd(i, 33) * 20;
            P(dc, 0xFF1A0E20, x - 14, y - 10, x + 14, y - 10, x + 2, y + 24 + Rnd(i, 34) * 30);
        }
        Strip(dc, 0xFF3A2440, 0, 112, 1024, 128, 3, 3);
        DrawLedges(dc, room, 0xFF26162C, 0xFF6E4258, 0xFF180C1E, 2);
        // The fallen log.
        foreach (var wall in room.Walls)
        {
            double x = wall.X, t = wall.Top, b = wall.Bottom;
            P(dc, 0xFF4A2E2A, x - 26, b, x - 24, t + 6, x - 14, t, x + 16, t, x + 24, t + 8, x + 26, b);
            P(dc, 0xFF6A4434, x - 18, t + 2, x + 14, t + 2, x + 18, t + 8, x - 20, t + 8);
            Ngon(dc, 0xFF8A5C40, x + 20, (t + b) / 2 + 2, 7, (b - t) / 2 - 2, 10);
            Ngon(dc, 0xFF5A3A2A, x + 20, (t + b) / 2 + 2, 3, (b - t) / 4, 8);
        }
    }

    private void DrawClearing(DrawingContext dc, Room room)
    {
        Bands(dc, 0, 420, Dusk);
        DrawMoons(dc, 760, 95, 40);
        Ridge(dc, 0xFF7A3456, 340, 46, 41, 60);
        Ridge(dc, 0xFF56264C, 395, 36, 43, 45);
        Ridge(dc, 0xFF341838, 455, 20, 45, 30);
        DrawLedges(dc, room, 0xFF26162C, 0xFF6E4258, 0xFF180C1E, 3);
        for (int i = 0; i < 26; i++)
        {
            double x = i * 42 + Rnd(i, 46) * 20, h = 30 + Rnd(i, 47) * 70;
            P(dc, 0xFF160A1A, x - 6, Sections.Surface + 3, x + 2 + Rnd(i, 48) * 14, Sections.Surface - h, x + 6, Sections.Surface + 3);
        }
    }

    /// <summary>
    /// The great view from the cliff (and behind the title): the low sun, the moons, the plain falling away in hazy
    /// bands with a river shining across it, and the alien city's spires far off on the horizon.
    /// </summary>
    private void DrawVista(DrawingContext dc, double t, bool title)
    {
        Bands(dc, 0, 470, Dusk);
        DrawMoons(dc, title ? 110 : 180, title ? 350 : 110, title ? 36 : 50);
        double sx = 760, sy = 440;
        Ngon(dc, 0x30F0A060, sx, sy, 190, 190, 32);
        Ngon(dc, 0x50F4B070, sx, sy, 140, 140, 32);
        Ngon(dc, 0xFFF6C27A, sx, sy, 104, 104, 32);
        Ngon(dc, 0xFFFAD898, sx - 12, sy - 14, 70, 70, 28);
        // The city, far off.
        for (int i = 0; i < 12; i++)
        {
            double x = 600 + i * 26 + Rnd(i, 51) * 10, hgt = 30 + Rnd(i, 52) * 70;
            P(dc, 0xFF9A4A5C, x - 6, 450, x - 3, 450 - hgt, x, 450 - hgt - 12, x + 3, 450 - hgt, x + 6, 450);
        }
        P(dc, 0xFF9A4A5C, 590, 452, 690, 452, 676, 430, 640, 420, 604, 430);
        // The plain, band after band.
        Ridge(dc, 0xFF8C3E58, 452, 10, 52, 80);
        Ridge(dc, 0xFF743452, 500, 16, 53, 70);
        Ridge(dc, 0xFF5C2A4C, 560, 22, 54, 60);
        P(dc, 0xFFE89A62, 1040, 520, 820, 540, 640, 560, 700, 575, 520, 600, 600, 620, 420, 660, 560, 676, 380, 720, 330, 720, 520, 676, 380, 660, 560, 620, 480, 600, 640, 575, 560, 560, 760, 538, 1040, 512);
        Ridge(dc, 0xFF441E42, 640, 26, 55, 50);
        Ridge(dc, 0xFF2E1434, 720, 22, 56, 40);
        double birds = (t * 18) % 1400 - 200;
        for (int i = 0; i < 3; i++)
        {
            double bx = birds + i * 40, by = 200 + i * 18 + Math.Sin(t * 2 + i) * 6, flap = Math.Sin(t * 8 + i) * 6;
            P(dc, 0xFF2A1430, bx - 12, by - flap, bx, by, bx + 12, by - flap, bx, by + 3);
        }
        if (title)
        {
            // A lone figure on a spur of rock, looking out.
            P(dc, 0xFF120A16, 0, 600, 120, 584, 250, 590, 330, 610, 300, 640, 360, 700, 300, 768, 0, 768);
            var b = new Body { Kind = Kind.Hero, Facing = 1, Act = Act.Stand, T = t };
            var silhouette = HeroBuild with { Skin = 0xFF120A16, SkinDark = 0xFF120A16, Top = 0xFF120A16, TopDark = 0xFF120A16, Legs = 0xFF120A16, LegsDark = 0xFF120A16, Feet = 0xFF120A16, Hair = 0xFF120A16, Trim = 0xFF120A16 };
            DrawPerson(dc, silhouette, new Placing(Breathe(Standing, t), 200, 588), 1, false, false, 0, false);
        }
    }

    // ───────────────────────────── the prison ─────────────────────────────

    /// <summary>The hall of the cage: tall windows on the sunset, pillars, and the cage on its chain.</summary>
    private void DrawHall(DrawingContext dc, World w)
    {
        Box(dc, 0xFF221A30, 0, 0, Field.Width, Field.Height);
        for (int i = 0; i < 3; i++)
        {
            double x = 120 + i * 300, wd = 180;
            P(dc, 0xFF14101E, x - 12, 600, x - 12, 210, x + wd / 2, 90, x + wd + 12, 210, x + wd + 12, 600);
            var sky = new uint[] { 0xFF8A3A50, 0xFFA84850, 0xFFC45A4C, 0xFFD87048, 0xFFE68C50 };
            for (int k = 0; k < sky.Length; k++)
            {
                double y0 = 120 + k * 92;
                using (dc.PushClip(new Rect(x, y0, wd, 93)))
                    P(dc, sky[k], x, 600, x, 220, x + wd / 2, 110, x + wd, 220, x + wd, 600);
            }
            for (int s = 0; s < 4; s++)
            {
                double bx = x + 20 + s * 44 + Rnd(s + i * 4, 61) * 10, bh = 60 + Rnd(s + i * 4, 62) * 120;
                P(dc, 0xFF6A2C48, bx - 9, 600, bx - 5, 600 - bh, bx, 600 - bh - 16, bx + 5, 600 - bh, bx + 9, 600);
            }
            Strip(dc, 0xFF14101E, x + wd / 2, 100, x + wd / 2, 600, 8, 8);
            Strip(dc, 0xFF14101E, x, 340, x + wd, 340, 6, 6);
        }
        for (int i = 0; i < 4; i++)
        {
            double x = 60 + i * 300;
            P(dc, 0xFF2E243E, x - 28, 640, x - 22, 70, x + 22, 70, x + 28, 640);
            P(dc, 0xFF1A1426, x + 8, 640, x + 12, 70, x + 22, 70, x + 28, 640);
            P(dc, 0xFF3A2E4C, x - 40, 70, x + 40, 70, x + 30, 50, x - 30, 50);
        }
        Box(dc, 0xFF16101E, 0, 0, Field.Width, 46);
        Strip(dc, 0xFF3A2E4C, 0, 46, Field.Width, 46, 6, 6);
        DrawLedges(dc, w.Def.Rooms[0], 0xFF2C2238, 0xFF5E4C70, 0xFF201828, 6);
        for (int i = 0; i < 16; i++) Strip(dc, 0xFF201828, i * 70, 650, i * 70 - 30, 768, 3, 3);
        var g = w.Foes[0];
        var cg = w.Cage;
        DrawBody(dc, w, g);
        // The chain and the cage.
        double cx = cg.Landed || cg.Broken ? cg.X : Sections.CageX + Sections.Chain * Math.Sin(cg.Theta);
        double cy = cg.Landed || cg.Broken ? cg.Y : Sections.CageTop + Sections.Chain * Math.Cos(cg.Theta);
        double tilt = cg.Broken ? cg.Theta * (cg.Landed ? 0.35 : 1) : cg.Theta;
        Ngon(dc, 0xFF5A4C6A, Sections.CageX, Sections.CageTop, 10, 10, 8);
        double ex = cg.Broken ? Sections.CageX + 60 * Math.Sin(cg.Theta) : cx, ey = cg.Broken ? Sections.CageTop + 60 * Math.Cos(cg.Theta) : cy - Sim.CageHalf;
        for (int i = 0; i < 14; i++)
        {
            double k0 = i / 14.0, k1 = (i + 0.8) / 14.0;
            Strip(dc, i % 2 == 0 ? 0xFF6A6078 : 0xFF4A4258, Sections.CageX + (ex - Sections.CageX) * k0, Sections.CageTop + (ey - Sections.CageTop) * k0,
                Sections.CageX + (ex - Sections.CageX) * k1, Sections.CageTop + (ey - Sections.CageTop) * k1, i % 2 == 0 ? 6 : 3, i % 2 == 0 ? 6 : 3);
        }
        DrawCage(dc, w, cx, cy, cg.Landed ? 0.18 : tilt, cg.Landed);
    }

    private void DrawCage(DrawingContext dc, World w, double cx, double cy, double tilt, bool wrecked)
    {
        double c = Math.Cos(tilt), s = Math.Sin(tilt), hw = 62, hh = Sim.CageHalf;
        Point M(double x, double y) => new(cx + x * c - y * s, cy + x * s + y * c);
        void Bar(double x0, double y0, double x1, double y1, uint col, double wd)
        {
            var (a, b) = (M(x0, y0), M(x1, y1));
            Strip(dc, col, a.X, a.Y, b.X, b.Y, wd, wd);
        }
        Poly(dc, 0x40000000, [M(-hw, -hh), M(hw, -hh), M(hw, hh), M(-hw, hh)]);
        for (int i = 0; i < 7; i++) Bar(-hw + i * hw * 2 / 6 + 4, -hh, -hw + i * hw * 2 / 6 + 4, hh, 0xFF3A3448, 4);
        // The two inside: the physicist, and the alien prisoner beside him.
        var h = w.Hero;
        double sit = wrecked ? 0 : 0;
        var heroPlace = new Placing(h.Act == Act.Dead ? Dying(w, h).Pose : Hanging with { Free = false, ArmN = 2.75, ArmF = 2.9, HipN = 0.05, HipF = -0.05 },
            M(-20, hh - 4 + sit).X, M(-20, hh - 4).Y, h.Act == Act.Dead ? Dying(w, h).Rot : tilt);
        var oruPlace = new Placing(Hanging with { Free = false, ArmN = 2.85, ArmF = 2.7, HipN = 0.02, HipF = -0.02, Lean = 0.05 }, M(26, hh - 4).X, M(26, hh - 4).Y, tilt);
        DrawPerson(dc, OruBuild, oruPlace with { Pose = oruPlace.Pose }, -1, false, false, 0, false);
        DrawPerson(dc, HeroBuild, heroPlace, 1, false, h.Act == Act.Dead && h.T < 0.36, 0, false);
        for (int i = 0; i < 7; i++) Bar(-hw + i * hw * 2 / 6, -hh, -hw + i * hw * 2 / 6, hh, i % 2 == 0 ? 0xFF8A8098 : 0xFF6E6680, 5);
        Poly(dc, 0xFF4E465E, [M(-hw - 6, -hh - 8), M(hw + 6, -hh - 8), M(hw + 6, -hh + 4), M(-hw - 6, -hh + 4)]);
        Poly(dc, 0xFF4E465E, [M(-hw - 6, hh - 4), M(hw + 6, hh - 4), M(hw + 6, hh + 8), M(-hw - 6, hh + 8)]);
        Poly(dc, 0xFF6E6680, [M(-hw - 6, -hh - 8), M(hw + 6, -hh - 8), M(hw + 6, -hh - 4), M(-hw - 6, -hh - 4)]);
        Poly(dc, 0xFF5A4C6A, [M(-12, -hh - 8), M(12, -hh - 8), M(6, -hh - 20), M(-6, -hh - 20)]);
        if (wrecked)
        {
            Bar(hw - 10, -hh, hw + 30, -hh - 30, 0xFF8A8098, 5);
            Bar(-hw + 20, hh, -hw - 30, hh - 10, 0xFF8A8098, 5);
        }
    }

    private void DrawCells(DrawingContext dc, World w, Room room)
    {
        bool gate = room.Look == Look.Gate;
        Box(dc, 0xFF1E2430, 0, 0, Field.Width, Field.Height);
        Box(dc, 0xFF141820, 0, 0, Field.Width, 90);
        Strip(dc, 0xFF3A4252, 0, 92, Field.Width, 92, 6, 6);
        for (int i = 0; i < 9; i++)
        {
            double x = i * 128;
            P(dc, 0xFF283040, x + 6, 100, x + 122, 100, x + 122, 610, x + 6, 610);
            P(dc, 0xFF222836, x + 96, 100, x + 122, 100, x + 122, 610, x + 96, 610);
            Strip(dc, 0xFF343C4E, x + 6, 330, x + 122, 330, 4, 4);
        }
        // Lamps and their light.
        for (int i = 0; i < 4; i++)
        {
            double x = 128 + i * 256;
            P(dc, 0x18FFD890, x - 20, 96, x + 20, 96, x + 130, 620, x - 130, 620);
            P(dc, 0xFFFFD890, x - 22, 88, x + 22, 88, x + 16, 98, x - 16, 98);
        }
        if (!gate)
        {
            for (int i = 0; i < 3; i++)
            {
                double x = 230 + i * 260;
                P(dc, 0xFF0C0E14, x, 610, x, 300, x + 40, 260, x + 120, 260, x + 160, 300, x + 160, 610);
                // A prisoner in the dark behind the bars.
                Ngon(dc, 0xFF1A1E28, x + 80 + i * 6, 360, 14, 18, 10);
                P(dc, 0xFF1A1E28, x + 50 + i * 6, 610, x + 60 + i * 6, 390, x + 100 + i * 6, 390, x + 110 + i * 6, 610);
                for (int b = 0; b < 6; b++)
                {
                    double bx = x + 14 + b * 26, flick = 0.55 + 0.35 * Math.Sin(_clock * 9 + b * 1.7 + i);
                    Strip(dc, Alpha(0xFF60D8F0, flick * 0.3), bx, 290, bx, 610, 9, 9);
                    Strip(dc, Alpha(0xFFC8F8FF, flick), bx, 290, bx, 610, 2.4, 2.4);
                }
                P(dc, 0xFF3A4252, x - 8, 300, x + 40, 252, x + 120, 252, x + 168, 300, x + 160, 306, x + 116, 264, x + 44, 264, x, 306);
            }
        }
        else
        {
            // The door, and the panel the friend works.
            double open = w.DoorOpen ? Smooth((w.Door - Sim.DoorSeconds) / 1.2) : 0;
            P(dc, 0xFF0A0C10, Sections.DoorX - 6, 610, Sections.DoorX - 6, 360, 1034, 360, 1034, 610);
            double dy = -open * 250;
            P(dc, 0xFF4A4458, Sections.DoorX - 4, 372 + dy, Sections.DoorX + 70, 372 + dy, Sections.DoorX + 70, 620 + dy, Sections.DoorX - 4, 620 + dy);
            for (int s = 0; s < 5; s++) P(dc, s % 2 == 0 ? 0xFFC0A030 : 0xFF2A2630, Sections.DoorX - 4, 400 + s * 40 + dy, Sections.DoorX + 70, 380 + s * 40 + dy, Sections.DoorX + 70, 396 + s * 40 + dy, Sections.DoorX - 4, 416 + s * 40 + dy);
            P(dc, 0xFF5E6676, Sections.DoorX - 14, 350, 1034, 350, 1034, 372, Sections.DoorX - 14, 372);
            double px = Sections.PanelX + 40;
            P(dc, 0xFF2A3040, px - 22, 620, px - 18, 470, px + 22, 470, px + 26, 620);
            P(dc, 0xFF3E4658, px - 28, 470, px + 28, 470, px + 22, 452, px - 22, 452);
            double done = Math.Min(1, w.Door / Sim.DoorSeconds);
            for (int k = 0; k < 6; k++)
            {
                bool lit = k < done * 6 || (k == (int)(done * 6) && (int)(_clock * 6) % 2 == 0);
                Box(dc, lit ? 0xFF70F0A0 : 0xFF1C3028, px - 16 + k * 5.6, 482, 4, 10);
            }
            Ngon(dc, w.DoorOpen ? 0xFF70F0A0 : 0xFFE04838, px, 510, 5, 5, 8);
        }
        DrawLedges(dc, room, 0xFF2E3644, 0xFF6A7488, 0xFF242A36, 7);
        for (int i = 0; i < 32; i++) Strip(dc, 0xFF262C38, i * 34, 640, i * 34, 768, 2, 2);
    }

    // ───────────────────────────── the caves ─────────────────────────────

    private static void Strata(DrawingContext dc, uint[] colours, int seed)
    {
        double h = Field.Height / (double)colours.Length;
        for (int i = 0; i < colours.Length; i++)
        {
            var pts = new List<Point> { new(-10, Field.Height + 10) };
            for (int k = 0; k <= 16; k++) pts.Add(new Point(k * 64, i * h + Math.Sin(k * 0.9 + i * 1.7 + seed) * 16 + Rnd(k + i * 20, seed) * 10));
            pts.Add(new Point(Field.Width + 10, Field.Height + 10));
            Poly(dc, colours[i], pts);
        }
    }

    private void DrawShaft(DrawingContext dc, Room room, bool top)
    {
        Strata(dc, [0xFF2E1A14, 0xFF38201A, 0xFF42261C, 0xFF3A2218, 0xFF4A2C1E, 0xFF3C2218, 0xFF2E1A14], top ? 70 : 71);
        // The dark of the shaft, and a beam of light falling down it.
        P(dc, 0xFF120A08, 600, -10, 960, -10, 1000, 768, 560, 768);
        P(dc, 0x22FFD8A0, 680, -10, 860, -10, 900, 768, 640, 768);
        for (int i = 0; i < 10; i++)
        {
            double y = (_clock * 18 + i * 77) % 768, x = 690 + Rnd(i, 72) * 180;
            Ngon(dc, 0x40FFE0B0, x, y, 1.6, 1.6, 4);
        }
        P(dc, 0xFF1E120E, 960, -10, 1034, -10, 1034, 778, 1000, 778);
        P(dc, 0xFF1E120E, -10, -10, 70, -10, 40, 300, 60, 778, -10, 778);
        DrawLedges(dc, room, 0xFF4A2C1E, 0xFFB07848, 0xFF32190F, top ? 8 : 9);
    }

    private void DrawGrotto(DrawingContext dc, Room room)
    {
        Strata(dc, [0xFF0E2026, 0xFF12282E, 0xFF163036, 0xFF12282E, 0xFF0E2228], 80);
        // The roof, its stalactites, and the acid that drips from three of them.
        var roof = new List<Point> { new(-10, -10), new(1034, -10) };
        for (int i = 24; i >= 0; i--) roof.Add(new Point(i * 1024 / 24.0, 90 + Rnd(i, 81) * 40));
        Poly(dc, 0xFF0A1418, roof);
        for (int i = 0; i < 18; i++)
        {
            double x = 20 + i * 58 + Rnd(i, 82) * 20;
            if (room.Drips.Any(d => Math.Abs(d - x) < 30)) continue;
            P(dc, 0xFF0A1418, x - 12, 100, x + 12, 100, x + 1, 150 + Rnd(i, 83) * 60);
        }
        foreach (double d in room.Drips)
        {
            P(dc, 0xFF0A1418, d - 18, 96, d + 18, 96, d + 3, Sections.DripRoof, d - 3, Sections.DripRoof);
            P(dc, 0xFF2E5A1E, d - 6, Sections.DripRoof - 18, d + 6, Sections.DripRoof - 18, d + 2, Sections.DripRoof, d - 2, Sections.DripRoof);
            Ngon(dc, 0x305AE040, d, Sections.GrottoFloor + 2, 30, 6, 12);
            P(dc, 0xFF4E9A2A, d - 22, Sections.GrottoFloor + 1, d + 22, Sections.GrottoFloor + 1, d + 14, Sections.GrottoFloor + 5, d - 14, Sections.GrottoFloor + 5);
        }
        for (int i = 0; i < 9; i++)
        {
            double x = 40 + i * 118 + Rnd(i, 84) * 30, y = 200 + Rnd(i, 85) * 360, pulse = 0.6 + 0.4 * Math.Sin(_clock * 1.5 + i);
            Ngon(dc, Alpha(0xFF60E0B0, 0.15 * pulse), x, y, 18, 18, 10);
            Ngon(dc, Alpha(0xFF90F0C8, 0.8 * pulse), x, y, 3.5, 3.5, 6);
        }
        DrawLedges(dc, room, 0xFF16303A, 0xFF4E8A8E, 0xFF0E2026, 10);
    }

    private void DrawSunken(DrawingContext dc, World w, Room room, int index)
    {
        Box(dc, 0xFF0A1216, 0, 0, Field.Width, Field.Height);
        Strata(dc, [0xFF0C161A, 0xFF101C20, 0xFF0C161A], 90 + index);
        var water = room.Water!.Value;
        uint[] deep = [0xFF1E5468, 0xFF1A4A5E, 0xFF164052, 0xFF123646, 0xFF0E2C3C, 0xFF0C2432];
        double bh = (water.Y1 - water.Y0) / deep.Length;
        for (int i = 0; i < deep.Length; i++) Box(dc, deep[i], water.X0, water.Y0 + i * bh, water.X1 - water.X0, bh + 1);
        // Light from where the water's open to the air.
        for (double x = water.X0; x < water.X1; x += 20)
        {
            if (room.Rocks.Any(r => r.Contains(x + 10, water.Y0 + 6))) continue;
            P(dc, 0x1880D0E0, x, water.Y0, x + 20, water.Y0, x + 60, water.Y1, x + 30, water.Y1);
        }
        for (int i = 0; i < 12; i++)
        {
            double x = Rnd(i, 91 + index) * 1024, y = water.Y1 - (_clock * 26 + i * 61) % (water.Y1 - water.Y0);
            if (!room.Rocks.Any(r => r.Touches(x, y, 6))) Ngon(dc, 0x50B0E8F0, x + Math.Sin(_clock * 2 + i) * 4, y, 2.2, 2.2, 6);
        }
        int n = 0;
        foreach (var rock in room.Rocks) DrawRock(dc, rock, 0xFF16262C, 0xFF223A40, 95 + index * 10 + n++);
        foreach (var l in room.Floors) DrawLedge(dc, l, 0xFF16262C, 0xFF4A7880, 0xFF101C20, 99 + index);
        Strip(dc, 0x8090D8E8, water.X0, water.Y0 + 1, water.X1, water.Y0 + 1, 2, 2);
        _ = w;
    }

    // ───────────────────────────── the power halls ─────────────────────────────

    private void DrawPlant(DrawingContext dc, Room room)
    {
        Box(dc, 0xFF12161C, 0, 0, Field.Width, Field.Height);
        // The great turbine turning behind.
        double cx = room.Look == Look.Plant ? 300 : 720, cy = 330, r = 230, spin = _clock * 0.4;
        Ngon(dc, 0xFF1A2028, cx, cy, r + 24, r + 24, 32);
        Ngon(dc, 0xFF20272F, cx, cy, r, r, 32);
        for (int i = 0; i < 12; i++)
        {
            double a = spin + i * Math.PI / 6;
            P(dc, 0xFF2A333D, cx + Math.Cos(a) * 30, cy + Math.Sin(a) * 30, cx + Math.Cos(a + 0.18) * r, cy + Math.Sin(a + 0.18) * r, cx + Math.Cos(a + 0.34) * r, cy + Math.Sin(a + 0.34) * r, cx + Math.Cos(a + 0.2) * 30, cy + Math.Sin(a + 0.2) * 30);
        }
        Ngon(dc, 0xFF3A4450, cx, cy, 40, 40, 12);
        Ngon(dc, 0xFF5AC8E0, cx, cy, 10, 10, 8);
        // Pipes along the top, gauges, the floor's grating.
        Box(dc, 0xFF1C2228, 0, 0, Field.Width, 70);
        for (int k = 0; k < 3; k++)
        {
            double y = 18 + k * 20;
            Box(dc, 0xFF2E3640, 0, y, Field.Width, 12);
            Box(dc, 0xFF46505C, 0, y + 2, Field.Width, 3);
        }
        for (int i = 0; i < 6; i++)
        {
            double x = 80 + i * 180;
            Box(dc, 0xFF283038, x - 10, 70, 20, 520);
            Box(dc, 0xFF3A4450, x - 10, 70, 6, 520);
            Ngon(dc, 0xFF2E3640, x, 160 + (i % 2) * 200, 16, 16, 12);
            Ngon(dc, 0xFFE0C060, x, 160 + (i % 2) * 200, 3, 3, 6);
        }
        foreach (var b in room.Barriers)
        {
            Box(dc, 0xFF3A4450, b.X - 18, 90, 36, 26);
            Box(dc, 0xFF3A4450, b.X - 18, Sections.PlantFloor - 24, 36, 24);
            Box(dc, 0xFF5A6674, b.X - 18, 90, 36, 5);
        }
        if (room.Panel is { } panel)
        {
            Box(dc, 0xFF2A323C, panel.X0 - 6, panel.Y0 - 10, panel.X1 - panel.X0 + 12, panel.Y1 - panel.Y0 + 20);
            Box(dc, 0xFF46505C, panel.X0, panel.Y0, panel.X1 - panel.X0, panel.Y1 - panel.Y0);
        }
        DrawLedges(dc, room, 0xFF262E38, 0xFF6A7684, 0xFF1C222A, 11);
        for (int i = 0; i < 40; i++) Strip(dc, 0xFF1C222A, i * 26, 636, i * 26, 768, 3, 3);
    }

    /// <summary>The barriers, on or off, and the switch panel's lamp: they stand in front of the room.</summary>
    private void DrawBarriers(DrawingContext dc, World w, Room room)
    {
        foreach (var b in room.Barriers)
        {
            bool on = Sim.BarrierOn(w, b);
            Ngon(dc, on ? 0xFF90F0FF : 0xFF602020, b.X, 110, 6, 4, 8);
            Ngon(dc, on ? 0xFF90F0FF : 0xFF602020, b.X, Sections.PlantFloor - 26, 6, 4, 8);
            if (!on) continue;
            Strip(dc, 0x3060D8FF, b.X, 116, b.X, Sections.PlantFloor - 24, 30, 30);
            var pts = new List<Point>();
            for (int k = 0; k <= 14; k++) pts.Add(new Point(b.X + (Rnd(k + (int)(_clock * 20) * 15, 101) - 0.5) * 12, 116 + k * (Sections.PlantFloor - 140) / 14.0));
            for (int k = 0; k < pts.Count - 1; k++) Strip(dc, 0xFFE8FFFF, pts[k].X, pts[k].Y, pts[k + 1].X, pts[k + 1].Y, 3, 3);
            Strip(dc, 0x90A0F0FF, b.X, 116, b.X, Sections.PlantFloor - 24, 6, 6);
        }
        if (room.Panel is { } panel)
        {
            bool off = w.Switch > 0;
            Ngon(dc, off ? 0xFF70F0A0 : 0xFFE04838, (panel.X0 + panel.X1) / 2, (panel.Y0 + panel.Y1) / 2, 10, 10, 10);
            Ngon(dc, Alpha(off ? 0xFF70F0A0 : 0xFFE04838, 0.25 + 0.15 * Math.Sin(_clock * 6)), (panel.X0 + panel.X1) / 2, (panel.Y0 + panel.Y1) / 2, 22, 22, 12);
        }
    }

    // ───────────────────────────── the city ─────────────────────────────

    /// <summary>The alien city: domes and needle towers in hazy layers under the dawn sky, its windows lit, flyers crossing.</summary>
    private void DrawCity(DrawingContext dc, double t, int salt)
    {
        Bands(dc, 0, 640, Dawn);
        Ngon(dc, 0x30FFD8A0, 300 + salt * 300, 470, 170, 170, 32);
        Ngon(dc, 0xFFFFE0A8, 300 + salt * 300, 470, 70, 70, 32);
        Ngon(dc, 0xFFE8D8E8, 820 - salt * 500, 120, 30, 30, 24);
        P(dc, 0xFFC0A8C8, 820 - salt * 500, 90, 850 - salt * 500, 120, 820 - salt * 500, 150, 834 - salt * 500, 120);
        void Layer(uint colour, uint window, double baseY, double scale, int seed, double spacing)
        {
            for (int i = 0; i < (int)(1100 / spacing); i++)
            {
                double x = -40 + i * spacing + Rnd(i, seed) * spacing * 0.5, hgt = (80 + Rnd(i, seed + 1) * 220) * scale, wd = (16 + Rnd(i, seed + 2) * 26) * scale;
                int kind = (int)(Rnd(i, seed + 3) * 3);
                if (kind == 0)
                {
                    // A needle tower with a bulb.
                    P(dc, colour, x - wd / 2, baseY, x - wd * 0.3, baseY - hgt, x, baseY - hgt - 40 * scale, x + wd * 0.3, baseY - hgt, x + wd / 2, baseY);
                    Ngon(dc, colour, x, baseY - hgt * 0.8, wd * 0.9, wd * 0.5, 10);
                }
                else if (kind == 1)
                {
                    // A dome on a drum.
                    Box(dc, colour, x - wd, baseY - hgt * 0.4, wd * 2, hgt * 0.4);
                    var dome = new List<Point>();
                    for (int k = 0; k <= 8; k++) dome.Add(new Point(x - wd * Math.Cos(k * Math.PI / 8), baseY - hgt * 0.4 - wd * 0.9 * Math.Sin(k * Math.PI / 8)));
                    Poly(dc, colour, dome);
                    Strip(dc, colour, x, baseY - hgt * 0.4 - wd * 0.9, x, baseY - hgt * 0.4 - wd * 0.9 - 30 * scale, 3 * scale, 1);
                }
                else
                {
                    // A stepped block, its tiers set back.
                    for (int k = 0; k < 3; k++) Box(dc, colour, x - wd * (1 - k * 0.25), baseY - hgt * (0.35 + k * 0.22), wd * 2 * (1 - k * 0.25), hgt * (0.35 + k * 0.22));
                }
                if (window != 0)
                    for (int k = 0; k < 5; k++)
                        if (Rnd(i * 7 + k, seed + 5) < 0.6) Box(dc, window, x - wd * 0.2 + (k % 2) * wd * 0.25, baseY - hgt * (0.15 + k * 0.12), 3 * scale, 4 * scale);
            }
        }
        Layer(0xFFB46A6E, 0, 470, 0.5, 120 + salt, 46);
        P(dc, 0x30F0C0A0, -10, 440, 1034, 430, 1034, 480, -10, 490);
        Layer(0xFF8A4A62, 0xFFFFC870, 520, 0.75, 130 + salt, 70);
        Layer(0xFF5E3052, 0xFFFFB860, 600, 1.0, 140 + salt, 110);
        // The flyers.
        for (int i = 0; i < 4; i++)
        {
            double x = ((t * (30 + i * 12) + i * 300) % 1300) - 140, y = 220 + i * 46 + Math.Sin(t + i) * 8;
            P(dc, 0xFF3A2042, x - 14, y, x + 14, y - 2, x + 6, y + 4, x - 8, y + 4);
            Ngon(dc, 0xFFFFD890, x - 12, y + 1, 1.8, 1.8, 4);
        }
        P(dc, 0x38F0B090, -10, 590, 1034, 580, 1034, 640, -10, 650);
        Box(dc, 0xFF3A2042, 0, 640, Field.Width, 128);
        Layer(0xFF2A1834, 0xFFE89848, 790, 1.05, 150 + salt, 120);
        P(dc, 0x30E8A080, -10, 700, 1034, 690, 1034, 768, -10, 768);
    }

    private static void DrawDeck(DrawingContext dc, Room room)
    {
        foreach (var l in room.Floors)
        {
            double x0 = l.X0 <= 0 ? -10 : l.X0, x1 = l.X1 >= Field.Width ? Field.Width + 10 : l.X1, y = l.Y0;
            P(dc, 0xFF3E2E48, x0, y, x1, y, x1, y + l.Thick, x0, y + l.Thick);
            P(dc, 0xFF9A88A8, x0, y, x1, y, x1, y + 6, x0, y + 6);
            P(dc, 0xFF5E4C6A, x0, y + 6, x1, y + 6, x1, y + 12, x0, y + 12);
            for (double x = Math.Ceiling(x0 / 64) * 64; x < x1; x += 64)
            {
                P(dc, 0xFF2A1E34, x - 3, y + 12, x + 3, y + 12, x + 3, y + l.Thick, x - 3, y + l.Thick);
                P(dc, 0xFF5E4C6A, x - 3, y, x + 3, y, x + 3, y - 54, x - 3, y - 54);
            }
            P(dc, 0xFF7A6888, x0, y - 54, x1, y - 54, x1, y - 49, x0, y - 49);
            P(dc, 0xFF5E4C6A, x0, y - 28, x1, y - 28, x1, y - 25, x0, y - 25);
            // Broken ends hang ragged over the drop.
            if (l.X1 < Field.Width) P(dc, 0xFF3E2E48, x1, y, x1 + 16, y + 10, x1 + 4, y + 22, x1 + 12, y + 40, x1, y + l.Thick);
            if (l.X0 > 0) P(dc, 0xFF3E2E48, x0, y, x0 - 18, y + 14, x0 - 6, y + 26, x0 - 10, y + 44, x0, y + l.Thick);
            P(dc, 0xFF2A1E34, x0, y + l.Thick, x1, y + l.Thick, (x0 + x1) / 2 + 30, y + l.Thick + 30, (x0 + x1) / 2 - 30, y + l.Thick + 30);
        }
    }

    private void DrawTower(DrawingContext dc)
    {
        Box(dc, 0xFF22141E, 0, 0, Field.Width, Field.Height);
        // The great round window, the city and the moons in it.
        double cx = 560, cy = 300, r = 220;
        var window = new List<Point>();
        for (int i = 0; i < 40; i++) window.Add(new Point(cx + Math.Cos(i * Math.PI / 20) * r, cy + Math.Sin(i * Math.PI / 20) * r));
        Ngon(dc, 0xFF3A2430, cx, cy, r + 26, r + 26, 40);
        using (dc.PushClip(new Rect(cx - r, cy - r, r * 2, r * 2)))
        using (dc.PushGeometryClip(Geometry(window)))
        {
            using (dc.PushTransform(Matrix.CreateScale(0.62, 0.62) * Matrix.CreateTranslation(cx - r - 40, cy - r + 10)))
                DrawCity(dc, _clock, 2);
            DrawMoons(dc, cx - 110, cy - 120, 30);
        }
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4;
            Strip(dc, 0xFF2A1A26, cx, cy, cx + Math.Cos(a) * r, cy + Math.Sin(a) * r, 8, 12);
        }
        Ngon(dc, 0xFF2A1A26, cx, cy, 30, 30, 12);
        Ngon(dc, 0xFFC8A048, cx, cy, 10, 10, 8);
        // Columns, banners.
        foreach (double x in new double[] { 140, 980 })
        {
            P(dc, 0xFF34202C, x - 34, 600, x - 28, 40, x + 28, 40, x + 34, 600);
            P(dc, 0xFF1E1018, x + 10, 600, x + 14, 40, x + 28, 40, x + 34, 600);
        }
        foreach (double x in new double[] { 250, 870 })
        {
            P(dc, 0xFF8A2030, x - 30, 60, x + 30, 60, x + 30, 380, x, 420, x - 30, 380);
            P(dc, 0xFFC8A048, x - 30, 60, x + 30, 60, x + 30, 70, x - 30, 70);
            P(dc, 0xFFC8A048, x - 8, 200, x + 8, 200, x, 230);
            P(dc, 0xFF5E1420, x + 10, 70, x + 30, 70, x + 30, 380, x + 10, 400);
        }
        Box(dc, 0xFF160C14, 0, 0, Field.Width, 36);
        Box(dc, 0xFF3A2632, 0, Sections.TowerFloor, Field.Width, Field.Height);
        Box(dc, 0xFF7A5868, 0, Sections.TowerFloor, Field.Width, 5);
        P(dc, 0x30C8A0B0, 300, Sections.TowerFloor + 10, 820, Sections.TowerFloor + 10, 760, 768, 360, 768);
        for (int i = 0; i < 12; i++) Strip(dc, 0xFF2C1C26, i * 96, Sections.TowerFloor + 6, i * 96 - 80, 768, 2, 2);
    }

    private static StreamGeometry Geometry(IReadOnlyList<Point> pts)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        c.BeginFigure(pts[0], true);
        for (int i = 1; i < pts.Count; i++) c.LineTo(pts[i]);
        c.EndFigure(true);
        return g;
    }

    // ───────────────────────────── in front of the room ─────────────────────────────

    /// <summary>Things standing in the room around the people: the drips, the barriers; the tentacles that catch him.</summary>
    private void DrawRoomThings(DrawingContext dc, World w, Room room, int index, bool front)
    {
        if (!front)
        {
            for (int i = 0; i < room.Drips.Count; i++)
            {
                double x = room.Drips[i], y = Sim.DripY(w, i);
                if (y < Sections.GrottoFloor)
                {
                    Ngon(dc, 0x4080F060, x, y, 9, 12, 8);
                    P(dc, 0xFFA0FF70, x - 4, y, x, y - 12, x + 4, y, x, y + 5);
                }
                else
                {
                    double k = Math.Min(1, (y - Sections.GrottoFloor) / 300);
                    for (int s = 0; s < 5; s++) Ngon(dc, Alpha(0xFFA0FF70, 1 - k), x + (s - 2) * 10 * (0.3 + k), Sections.GrottoFloor - 12 * Math.Sin(Math.PI * Math.Min(1, k * 1.5)) * (1 - Math.Abs(s - 2) * 0.3), 2.5, 2.5, 5);
                    double grow = Math.Clamp(((w.Clock + i * Sim.DripGap) % Sim.DripPeriod) / Sim.DripPeriod, 0, 1);
                    Ngon(dc, 0xFFA0FF70, x, Sections.DripRoof + 2, 2 + grow * 3, 3 + grow * 4, 6);
                }
            }
            return;
        }
        if (room.Tentacles) DrawTentacles(dc, w, front: true);
        if (room.Barriers.Count > 0 || room.Panel != null) DrawBarriers(dc, w, room);
        _ = index;
    }

    /// <summary>Over everything: the water tinting whoever is in it, and its surface.</summary>
    private void DrawForeground(DrawingContext dc, World w, Room room)
    {
        if (room.Water is not { } water) return;
        double wave = Math.Sin(_clock * 2) * 2;
        var top = new List<Point>();
        for (int i = 0; i <= 32; i++)
        {
            double x = water.X0 + (water.X1 - water.X0) * i / 32;
            top.Add(new Point(x, water.Y0 + Math.Sin(x * 0.05 + _clock * 3) * 2 + wave * 0.3));
        }
        var tint = new List<Point>(top) { new(water.X1, water.Y1), new(water.X0, water.Y1) };
        using (dc.PushClip(new Rect(water.X0, water.Y0 - 6, water.X1 - water.X0, water.Y1 - water.Y0 + 6)))
        {
            Poly(dc, 0x4A1A5A80, tint);
            for (int i = 0; i < top.Count - 1; i++)
                if (!room.Rocks.Any(r => r.Contains(top[i].X, water.Y0 + 6))) Strip(dc, 0xB0A8E0F0, top[i].X, top[i].Y, top[i + 1].X, top[i + 1].Y, 2.5, 2.5);
        }
        var h = w.Hero;
        if (h.Act == Act.Swim && h.Y - 44 > water.Y0)
            for (int i = 0; i < 3; i++)
            {
                double k = (_clock * 0.9 + i * 0.33) % 1;
                Ngon(dc, Alpha(0xFFD0F0FF, 0.7 * (1 - k)), h.X + h.Facing * 14 + Math.Sin(k * 9 + i) * 4, h.Y - 50 - k * 70, 2.5 + k * 2, 2.5 + k * 2, 6);
            }
    }

    // ───────────────────────────── shots, shields and sparks ─────────────────────────────

    private void DrawBolt(DrawingContext dc, World w, Bolt b)
    {
        double x = b.X, y = b.Y, d = b.Dir;
        if (b.Super)
        {
            double fl = 1 + Math.Sin(w.Clock * 60) * 0.12;
            P(dc, 0x50A0D8FF, x - d * 90, y, x - d * 30, y - 20 * fl, x + d * 26, y, x - d * 30, y + 20 * fl);
            P(dc, 0xC0E0F4FF, x - d * 70, y, x - d * 20, y - 11 * fl, x + d * 18, y, x - d * 20, y + 11 * fl);
            P(dc, 0xFFFFFFFF, x - d * 44, y, x - d * 10, y - 5, x + d * 12, y, x - d * 10, y + 5);
            return;
        }
        uint glow = b.Side == Side.Hero ? 0x80FFC060 : 0x80FF6070;
        P(dc, glow, x - d * 62, y - 4, x + d * 6, y - 6, x + d * 14, y, x + d * 6, y + 6, x - d * 62, y + 4);
        P(dc, 0xFFFFF8E0, x - d * 46, y - 1.6, x + d * 8, y - 2.4, x + d * 10, y, x + d * 8, y + 2.4, x - d * 46, y + 1.6);
    }

    private void DrawShield(DrawingContext dc, World w, Shield s, bool back)
    {
        double grow = Smooth(s.Age / 0.18), top = s.Y - Sim.ShieldHeight * grow;
        double struck = Math.Max(0, 1 - (w.Clock - s.Struck) / 0.2);
        double fade = double.IsPositiveInfinity(s.Life) || s.Life > 1 ? 1 : (int)(w.Clock * 14) % 2 == 0 ? 0.35 : 0.8;
        uint tint = s.Side == Side.Hero ? 0xFF9CE8FF : 0xFFFFA8E0;
        if (back)
        {
            P(dc, Alpha(tint, (0.16 + struck * 0.25) * fade), s.X - 18, s.Y, s.X - 14, top, s.X + 14, top, s.X + 18, s.Y);
            return;
        }
        var left = new List<Point>();
        for (int k = 0; k <= 12; k++)
        {
            double y = s.Y - (s.Y - top) * k / 12.0;
            left.Add(new Point(s.X - 4 - 2.5 * Math.Sin(k * 1.9 + w.Clock * 30), y));
        }
        for (int k = 12; k >= 0; k--)
        {
            double y = s.Y - (s.Y - top) * k / 12.0;
            left.Add(new Point(s.X + 4 + 2.5 * Math.Sin(k * 2.3 + w.Clock * 26), y));
        }
        Poly(dc, Alpha(tint, (0.5 + struck * 0.4) * fade), left);
        Strip(dc, Alpha(0xFFFFFFFF, (0.6 + struck * 0.4) * fade), s.X, s.Y, s.X, top, 1.6, 1.6);
        for (int k = 0; k < 4; k++)
        {
            double y = s.Y - ((w.Clock * 120 + k * 37) % Math.Max(1, s.Y - top));
            Strip(dc, Alpha(0xFFFFFFFF, 0.5 * fade), s.X - 8, y, s.X + 8, y, 1.4, 1.4);
        }
    }

    private void DrawSpark(DrawingContext dc, World w, Spark s)
    {
        double age = w.Clock - s.Born;
        switch (s.Kind)
        {
            case SparkKind.Block or SparkKind.Sparks:
            {
                if (age > 0.3) return;
                double k = age / 0.3;
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4 + Rnd(i, (int)(s.Born * 100)) * 0.6, r0 = 6 + k * 20, r1 = r0 + 12 * (1 - k);
                    Strip(dc, Alpha(s.Kind == SparkKind.Block ? 0xFFE8FFFF : 0xFFFFE0A0, 1 - k), s.X + Math.Cos(a) * r0, s.Y + Math.Sin(a) * r0, s.X + Math.Cos(a) * r1, s.Y + Math.Sin(a) * r1, 2.5, 1);
                }
                Ngon(dc, Alpha(0xFFFFFFFF, 1 - k), s.X, s.Y, 8 * (1 - k) + 2, 8 * (1 - k) + 2, 8);
                break;
            }
            case SparkKind.Hit:
            {
                if (age > 0.35) return;
                double k = age / 0.35;
                Ngon(dc, Alpha(0xFFFFF0D0, 0.7 * (1 - k)), s.X, s.Y, 20 + k * 40, 20 + k * 40, 12, k);
                Ngon(dc, Alpha(0xFFFFFFFF, 1 - k), s.X, s.Y, 10 * (1 - k) + 3, 10 * (1 - k) + 3, 8);
                break;
            }
            case SparkKind.Burst:
            {
                if (age > 0.6) return;
                double k = age / 0.6;
                for (int i = 0; i < 10; i++)
                {
                    double a = Rnd(i, 7) * Math.PI * 2, r = 10 + k * (40 + Rnd(i, 8) * 60), x = s.X + Math.Cos(a) * r, y = s.Y + Math.Sin(a) * r * 1.6 + k * k * 60;
                    P(dc, Alpha(0xFFB8F0FF, 1 - k), x - 4, y, x, y - 7, x + 4, y, x, y + 7);
                }
                break;
            }
            case SparkKind.Splash:
            {
                if (age > 0.8) return;
                double k = age / 0.8;
                for (int i = 0; i < 9; i++)
                {
                    double vx = (i - 4) * 32, vy = -220 - Rnd(i, 3) * 160, x = s.X + vx * age, y = s.Y + vy * age + 700 * age * age;
                    if (y < s.Y + 2) Ngon(dc, Alpha(0xFFC8ECFF, 1 - k), x, y, 3.5, 4.5, 6);
                }
                P(dc, Alpha(0xFFC8ECFF, 0.6 * (1 - k)), s.X - 30 - k * 40, s.Y, s.X + 30 + k * 40, s.Y, s.X + 20, s.Y - 6, s.X - 20, s.Y - 6);
                break;
            }
            case SparkKind.Squish:
            {
                if (age > 0.6) return;
                double k = age / 0.6;
                for (int i = 0; i < 6; i++)
                {
                    double x = s.X + (i - 2.5) * 10 * (0.5 + k), y = s.Y - Math.Sin(Math.PI * k) * (14 + i % 3 * 6);
                    Ngon(dc, Alpha(0xFF8AA03A, 1 - k), x, y, 3, 3, 5);
                }
                break;
            }
            case SparkKind.Dust:
            {
                if (age > 1.2) return;
                double k = age / 1.2;
                for (int i = 0; i < 12; i++)
                {
                    double x = s.X + (i - 5.5) * 16 * (0.6 + k * 1.6), y = s.Y - 10 - Rnd(i, 4) * 40 * k;
                    Ngon(dc, Alpha(0xFF8A7A90, 0.7 * (1 - k)), x, y, 14 + k * 26, 10 + k * 16, 7, i);
                }
                break;
            }
        }
    }
}

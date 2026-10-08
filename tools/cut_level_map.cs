// Cuts the level map (map-images/level_map.png, 1672 x 941) into the pieces LevelMapScreen.cs lays out.
//
// - Backgrounds/level_map.png: the picture with everything that depends on progress removed and rebuilt from the
//   surrounding art: the level circles, the golden and dashed paths, "Пройдено 6 из 15", the bar's fill, "40%",
//   the chapter name on the card and the caption of the big button.
//   Backgrounds/level_map_blur.png: a small blurred copy that fills the screen around it.
// - UI/map_node_done.png, map_node_current.png, map_node_locked.png: the three kinds of circle (passed with its
//   tick, the current one, locked with its padlock), number cleared, centred in a 97 x 97 square.
// - UI/map_helmet.png: the explorer's helmet that marks the chosen level.
// - UI/map_fill.png, map_fill_cap.png: the golden bar fill and its rounded end.
// - UI/map_play.png: the big button, laid over its painted place to darken when pressed.
//
// Run from the repository root (needs the .NET 10 SDK):  dotnet run tools/cut_level_map.cs
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#pragma warning disable CA1416

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

const string Source = "map-images/level_map.png";
const string Backgrounds = "Assets/Resources/Backgrounds/";
const string UI = "Assets/Resources/UI/";

Img src = Img.Load(Source);
Img clean = src.Clone();

// ---- The painted circles (picture pixels): centre of each, and which kind it shows on the mock-up.
var done = new[] { (407, 155), (662, 180), (895, 196), (480, 307), (737, 320), (980, 304) };
var current = (411, 434);
var locked = new[] { (680, 450), (951, 466), (419, 585), (684, 600), (924, 600), (520, 732), (721, 744), (989, 746) };

// ---- Sprites, cut before anything is cleared.
const int Half = 48;

// Passed: the gold ring and its tick badge.
{
    var (cx, cy) = done[0];
    Img piece = src.Crop(cx - Half, cy - Half, 2 * Half + 1, 2 * Half + 1);
    Smooth(piece, (x, y) => Dist(x, y, Half, Half) <= 18.5);
    piece.MaskAlpha((x, y) => Math.Max(Cover(Dist(x, y, Half, Half), 30.5), Cover(Dist(x, y, Half + 30, Half + 18), 13.5)));
    piece.Save(UI + "map_node_done.png");
}

// Current: the cyan ring. Its top is hidden under the helmet, so the bottom half is mirrored up.
{
    var (cx, cy) = current;
    Img piece = src.Crop(cx - Half, cy - Half, 2 * Half + 1, 2 * Half + 1);
    for (int y = 0; y < Half; y++)
        for (int x = 0; x <= 2 * Half; x++)
            piece.Copy(x, 2 * Half - y, x, y);
    Smooth(piece, (x, y) => Dist(x, y, Half, Half) <= 22.5);
    piece.MaskAlpha((x, y) => Cover(Dist(x, y, Half, Half), 37.5));
    piece.Save(UI + "map_node_current.png");
}

// Locked: the grey ring and its padlock (body 670..691 x 474..493, shackle above it inside the ring).
{
    var (cx, cy) = locked[0];
    Img piece = src.Crop(cx - Half, cy - Half, 2 * Half + 1, 2 * Half + 1);
    Smooth(piece, (x, y) => Dist(x, y, Half, Half) <= 19.5 && y - Half <= 13);
    piece.MaskAlpha((x, y) => Math.Max(Cover(Dist(x, y, Half, Half), 34.5),
        RoundRect(x + cx - Half, y + cy - Half, 670, 474, 691, 493, 3f)));
    piece.Save(UI + "map_node_locked.png");
}

// The helmet: brown and brass against the blue water behind it; the golden path running into it is left out.
{
    const int X0 = 368, Y0 = 372, W = 80, H = 56;
    Img piece = src.Crop(X0, Y0, W, H);
    var keep = new bool[W * H];
    for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            int px = x + X0, py = y + Y0;
            Color4 c = piece.Get(x, y);
            bool warm = c.R > c.B + 0.04f;
            bool inHull = Ellipse(px, py, 408, 398, 37, 27) || (py >= 405 && py <= 425 && px >= 380 && px <= 447);
            bool path = px < 384 && py > 410;
            keep[y * W + x] = warm && inHull && !path;
        }
    keep = LargestComponent(keep, W, H);
    keep = FillHoles(keep, W, H);
    piece.MaskAlpha((x, y) => keep[y * W + x] ? 1f : 0f);
    piece.Feather(1);
    piece.Save(UI + "map_helmet.png");
    Console.WriteLine($"helmet offset from its circle: {X0 + W / 2f - current.Item1}, {Y0 + H / 2f - current.Item2}");
}

// The golden fill of the bar: a slice of its body and its rounded end.
src.Crop(880, 113, 4, 19).Save(UI + "map_fill.png");
src.Crop(930, 113, 18, 19).Save(UI + "map_fill_cap.png");

// ---- What goes from the picture.
var hole = new bool[src.W * src.H];

// The golden circles glow well beyond their rim.
foreach (var (cx, cy) in done)
{
    MarkCircle(hole, cx, cy, 45);
    MarkCircle(hole, cx + 30, cy + 18, 18);
}
MarkCircle(hole, current.Item1, current.Item2, 45);
MarkRect(hole, 366, 370, 450, 428);
foreach (var (cx, cy) in locked)
{
    MarkCircle(hole, cx, cy, 36);
    MarkRect(hole, cx - 14, cy + 12, cx + 15, cy + 46);
}

var golden = new List<(float, float)[]>
{
    new[] { (366f, 166f), (380f, 161f) },
    new[] { (436f, 166f), (500f, 168f), (560f, 172f), (634f, 179f) },
    new[] { (692f, 182f), (760f, 188f), (820f, 194f), (866f, 198f) },
    new[] { (928f, 211f), (975f, 222f), (1005f, 234f), (1022f, 247f), (1026f, 260f), (1020f, 272f), (1007f, 281f), (992f, 291f) },
    new[] { (950f, 312f), (900f, 319f), (840f, 324f), (770f, 326f) },
    new[] { (705f, 318f), (650f, 312f), (600f, 308f), (512f, 310f) },
    new[] { (452f, 321f), (412f, 337f), (375f, 355f), (342f, 370f), (329f, 385f), (332f, 400f), (350f, 415f), (374f, 423f) },
};
var dashed = new List<(float, float)[]>
{
    new[] { (444f, 441f), (500f, 442f), (560f, 445f), (648f, 448f) },
    new[] { (712f, 456f), (770f, 468f), (825f, 475f), (870f, 472f), (918f, 467f) },
    new[] { (450f, 586f), (510f, 596f), (580f, 607f), (652f, 607f) },
    new[] { (716f, 613f), (760f, 610f), (800f, 600f), (850f, 594f), (893f, 600f) },
    new[] { (956f, 612f), (995f, 632f), (1030f, 650f), (1048f, 667f), (1062f, 675f), (1072f, 688f), (1072f, 703f),
            (1065f, 717f), (1055f, 727f), (1042f, 737f), (1020f, 744f) },
    new[] { (552f, 737f), (620f, 742f), (690f, 747f) },
    new[] { (753f, 752f), (800f, 757f), (850f, 762f), (900f, 758f), (958f, 751f) },
};
foreach (var line in golden) MarkPath(hole, line, 14f);
foreach (var line in dashed) MarkPath(hole, line, 7f);

var everywhere = new Rectangle(0, 0, src.W, src.H);
Inpaint(clean, hole, everywhere, 70);

// "Пройдено 6 из 15" and "40%" on the dark plate under the title.
var plate = new bool[src.W * src.H];
MarkRect(plate, 588, 104, 798, 139);
Inpaint(clean, plate, new Rectangle(520, 98, 720, 46), 320);

// "40%" sits on plain dark: filled in smoothly, as patches would bring in the tip of the bar.
Smooth(clean, (x, y) => x >= 1131 && x <= 1194 && y >= 102 && y <= 140);

// The bar keeps its empty track: its dark inside is the same all along, so the fill is covered with a column of it.
for (int y = 113; y <= 131; y++)
    for (int x = 818; x <= 950; x++)
        clean.Set(x, y, src.Get(1050, y));

// The chapter name on the card, rebuilt from the plain stone above it (the dividers would be copied otherwise).
var card = new bool[src.W * src.H];
MarkRect(card, 1370, 176, 1510, 218);
MarkRect(card, 1316, 248, 1580, 336);
Inpaint(clean, card, new Rectangle(1262, 140, 352, 37), 220);

// The caption of the big button: its gold is smooth, so it is filled in from the edges.
Smooth(clean, (x, y) => x >= 584 && x <= 1090 && y >= 829 && y <= 876);

clean.Save(Backgrounds + "level_map.png");
clean.Blurred(6).Save(Backgrounds + "level_map_blur.png");

// The big button over its painted place, shaped like the banner.
{
    const int X0 = 494, Y0 = 807, W = 685, H = 93;
    Img piece = clean.Crop(X0, Y0, W, H);
    var shape = new[] { new PointF(494, 855), new PointF(541, 808), new PointF(1131, 808), new PointF(1178, 855), new PointF(1131, 899), new PointF(541, 899) };
    piece.MaskAlpha((x, y) => Inside(shape, x + X0 + 0.5f, y + Y0 + 0.5f) ? 1f : 0f);
    piece.Save(UI + "map_play.png");
}

// A check picture with the cleared areas tinted, for looking over the masks.
{
    Img check = src.Clone();
    for (int i = 0; i < hole.Length; i++)
        if (hole[i] || plate[i] || card[i])
        {
            check.R[i] = check.R[i] * 0.5f + 0.5f;
            check.B[i] *= 0.5f;
        }
    Directory.CreateDirectory("Temp");
    check.Save("Temp/level_map_masks.png");
}

Console.WriteLine("level map pieces written");

// ================================================================================================ helpers

static double Dist(double x, double y, double cx, double cy) => Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

// 1 inside the radius, fading to 0 over one pixel.
static float Cover(double d, double r) => (float)Math.Clamp(r - d + 0.5, 0, 1);

static bool Ellipse(int x, int y, int cx, int cy, int rx, int ry) =>
    (x - cx) * (x - cx) / (double)(rx * rx) + (y - cy) * (y - cy) / (double)(ry * ry) <= 1;

static float RoundRect(int x, int y, int x0, int y0, int x1, int y1, float r)
{
    float dx = Math.Max(Math.Max(x0 + r - x, x - (x1 - r)), 0);
    float dy = Math.Max(Math.Max(y0 + r - y, y - (y1 - r)), 0);
    return Cover(Math.Sqrt(dx * dx + dy * dy), r);
}

static bool Inside(PointF[] poly, float x, float y)
{
    bool inside = false;
    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        if ((poly[i].Y > y) != (poly[j].Y > y) &&
            x < (poly[j].X - poly[i].X) * (y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
            inside = !inside;
    return inside;
}

static void MarkCircle(bool[] mask, int cx, int cy, int r)
{
    for (int y = cy - r; y <= cy + r; y++)
        for (int x = cx - r; x <= cx + r; x++)
            if (Dist(x, y, cx, cy) <= r)
                mask[y * Img.Width + x] = true;
}

static void MarkRect(bool[] mask, int x0, int y0, int x1, int y1)
{
    for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            mask[y * Img.Width + x] = true;
}

// A band along a smooth curve through the points.
static void MarkPath(bool[] mask, (float x, float y)[] points, float halfWidth)
{
    foreach (var (px, py) in Spline(points, 1.5f))
    {
        int r = (int)Math.Ceiling(halfWidth);
        for (int y = (int)py - r; y <= (int)py + r; y++)
            for (int x = (int)px - r; x <= (int)px + r; x++)
                if (Dist(x, y, px, py) <= halfWidth)
                    mask[y * Img.Width + x] = true;
    }
}

// Catmull-Rom through the points, sampled about every `step` pixels.
static List<(float, float)> Spline((float x, float y)[] p, float step)
{
    var result = new List<(float, float)>();
    for (int i = 0; i < p.Length - 1; i++)
    {
        var p0 = p[Math.Max(i - 1, 0)]; var p1 = p[i]; var p2 = p[i + 1]; var p3 = p[Math.Min(i + 2, p.Length - 1)];
        int n = Math.Max(2, (int)(Dist(p1.x, p1.y, p2.x, p2.y) / step));
        for (int k = 0; k < n; k++)
        {
            float t = k / (float)n, t2 = t * t, t3 = t2 * t;
            float x = 0.5f * (2 * p1.x + (-p0.x + p2.x) * t + (2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * t2 + (-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * t3);
            float y = 0.5f * (2 * p1.y + (-p0.y + p2.y) * t + (2 * p0.y - 5 * p1.y + 4 * p2.y - p3.y) * t2 + (-p0.y + 3 * p1.y - 3 * p2.y + p3.y) * t3);
            result.Add((x, y));
        }
    }
    result.Add(p[^1]);
    return result;
}

static bool[] LargestComponent(bool[] m, int w, int h)
{
    var label = new int[w * h];
    int best = 0, bestSize = 0, next = 0;
    for (int i = 0; i < m.Length; i++)
    {
        if (!m[i] || label[i] != 0) continue;
        next++;
        int size = 0;
        var stack = new Stack<int>();
        stack.Push(i); label[i] = next;
        while (stack.Count > 0)
        {
            int j = stack.Pop(); size++;
            int x = j % w, y = j / w;
            foreach (int k in new[] { x > 0 ? j - 1 : -1, x < w - 1 ? j + 1 : -1, y > 0 ? j - w : -1, y < h - 1 ? j + w : -1 })
                if (k >= 0 && m[k] && label[k] == 0) { label[k] = next; stack.Push(k); }
        }
        if (size > bestSize) { bestSize = size; best = next; }
    }
    var result = new bool[m.Length];
    for (int i = 0; i < m.Length; i++) result[i] = label[i] == best;
    return result;
}

// Everything not reachable from the border without crossing the mask belongs to it.
static bool[] FillHoles(bool[] m, int w, int h)
{
    var outside = new bool[m.Length];
    var stack = new Stack<int>();
    for (int x = 0; x < w; x++) { stack.Push(x); stack.Push((h - 1) * w + x); }
    for (int y = 0; y < h; y++) { stack.Push(y * w); stack.Push(y * w + w - 1); }
    while (stack.Count > 0)
    {
        int j = stack.Pop();
        if (m[j] || outside[j]) continue;
        outside[j] = true;
        int x = j % w, y = j / w;
        if (x > 0) stack.Push(j - 1);
        if (x < w - 1) stack.Push(j + 1);
        if (y > 0) stack.Push(j - w);
        if (y < h - 1) stack.Push(j + w);
    }
    var result = new bool[m.Length];
    for (int i = 0; i < m.Length; i++) result[i] = !outside[i];
    return result;
}

// Fills the region smoothly from its edges: rows are first blended between their two ends, then relaxed.
static void Smooth(Img img, Func<int, int, bool> region)
{
    int w = img.W, h = img.H;
    var inside = new bool[w * h];
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            inside[y * w + x] = region(x, y);
    for (int y = 0; y < h; y++)
    {
        int x = 0;
        while (x < w)
        {
            if (!inside[y * w + x]) { x++; continue; }
            int start = x;
            while (x < w && inside[y * w + x]) x++;
            Color4 a = img.Get(Math.Max(start - 1, 0), y), b = img.Get(Math.Min(x, w - 1), y);
            for (int k = start; k < x; k++)
                img.Set(k, y, Color4.Lerp(a, b, (k - start + 1f) / (x - start + 1f)));
        }
    }
    for (int iteration = 0; iteration < 600; iteration++)
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                int i = y * w + x;
                if (!inside[i]) continue;
                img.R[i] = (img.R[i - 1] + img.R[i + 1] + img.R[i - w] + img.R[i + w]) * 0.25f;
                img.G[i] = (img.G[i - 1] + img.G[i + 1] + img.G[i - w] + img.G[i + w]) * 0.25f;
                img.B[i] = (img.B[i - 1] + img.B[i + 1] + img.B[i - w] + img.B[i + w]) * 0.25f;
            }
}

// Exemplar-based inpainting (Criminisi et al.): the hole is filled from its edge inwards, most certain and most
// structured spots first, each 9 x 9 patch copied from the best-matching intact patch nearby inside `source`.
static void Inpaint(Img img, bool[] holeIn, Rectangle source, int search)
{
    const int P = 4;
    int w = img.W, h = img.H;
    var hole = (bool[])holeIn.Clone();
    var conf = new float[w * h];
    for (int i = 0; i < hole.Length; i++) conf[i] = hole[i] ? 0f : 1f;

    // Patches that may serve as a source: wholly intact and inside the source rectangle.
    var sum = new int[(w + 1) * (h + 1)];
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            sum[(y + 1) * (w + 1) + x + 1] = (holeIn[y * w + x] ? 1 : 0) + sum[y * (w + 1) + x + 1] + sum[(y + 1) * (w + 1) + x] - sum[y * (w + 1) + x];
    bool Valid(int x, int y)
    {
        if (x - P < source.Left || y - P < source.Top || x + P >= source.Right || y + P >= source.Bottom) return false;
        if (x - P < 0 || y - P < 0 || x + P >= w || y + P >= h) return false;
        int x0 = x - P, y0 = y - P, x1 = x + P + 1, y1 = y + P + 1;
        return sum[y1 * (w + 1) + x1] - sum[y0 * (w + 1) + x1] - sum[y1 * (w + 1) + x0] + sum[y0 * (w + 1) + x0] == 0;
    }

    // Work on the hole's bounding box.
    int bx0 = w, by0 = h, bx1 = 0, by1 = 0, left = 0;
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            if (hole[y * w + x]) { left++; bx0 = Math.Min(bx0, x); bx1 = Math.Max(bx1, x); by0 = Math.Min(by0, y); by1 = Math.Max(by1, y); }

    var front = new List<int>();
    while (left > 0)
    {
        front.Clear();
        for (int y = by0; y <= by1; y++)
            for (int x = bx0; x <= bx1; x++)
            {
                int i = y * w + x;
                if (hole[i] && ((x > 0 && !hole[i - 1]) || (x < w - 1 && !hole[i + 1]) || (y > 0 && !hole[i - w]) || (y < h - 1 && !hole[i + w])))
                    front.Add(i);
            }

        // Fill several far-apart front points per pass: the box is rescanned less often.
        front.Sort((a, b) => Priority(b).CompareTo(Priority(a)));
        var taken = new List<(int, int)>();
        foreach (int i in front)
        {
            if (taken.Count >= 24) break;
            int x = i % w, y = i / w;
            if (!hole[i]) continue;
            bool near = false;
            foreach (var (tx, ty) in taken)
                if (Math.Abs(tx - x) <= 2 * P + 1 && Math.Abs(ty - y) <= 2 * P + 1) { near = true; break; }
            if (near) continue;
            taken.Add((x, y));
            FillAt(x, y);
        }
    }

    float Priority(int i)
    {
        int x = i % w, y = i / w;
        float c = 0; int n = 0; float grad = 0;
        for (int dy = -P; dy <= P; dy++)
            for (int dx = -P; dx <= P; dx++)
            {
                int qx = x + dx, qy = y + dy;
                if (qx < 1 || qy < 1 || qx >= w - 1 || qy >= h - 1) continue;
                int q = qy * w + qx;
                n++;
                c += conf[q];
                if (!hole[q] && !hole[q - 1] && !hole[q + 1] && !hole[q - w] && !hole[q + w])
                {
                    float gx = img.Lum(q + 1) - img.Lum(q - 1), gy = img.Lum(q + w) - img.Lum(q - w);
                    grad = Math.Max(grad, gx * gx + gy * gy);
                }
            }
        return c / n * (0.15f + MathF.Sqrt(grad));
    }

    void FillAt(int x, int y)
    {
        double best = double.MaxValue; int bx = -1, by = -1;
        for (int sy = y - search; sy <= y + search; sy++)
            for (int sx = x - search; sx <= x + search; sx++)
            {
                if (!Valid(sx, sy)) continue;
                double d = 0;
                for (int dy = -P; dy <= P && d < best; dy++)
                    for (int dx = -P; dx <= P; dx++)
                    {
                        int tx = x + dx, ty = y + dy;
                        if (tx < 0 || ty < 0 || tx >= w || ty >= h) continue;
                        int t = ty * w + tx;
                        if (hole[t]) continue;
                        int s = (sy + dy) * w + sx + dx;
                        float r = img.R[t] - img.R[s], g = img.G[t] - img.G[s], b = img.B[t] - img.B[s];
                        d += r * r + g * g + b * b;
                    }
                // A slight preference for nearby patches keeps the texture local.
                d += 0.00002 * ((sx - x) * (sx - x) + (sy - y) * (sy - y));
                if (d < best) { best = d; bx = sx; by = sy; }
            }
        if (bx < 0) { bx = x; by = y; }

        int filledHere = 0; float c = 0;
        for (int dy = -P; dy <= P; dy++)
            for (int dx = -P; dx <= P; dx++)
            {
                int tx = x + dx, ty = y + dy;
                if (tx < 0 || ty < 0 || tx >= w || ty >= h) continue;
                c += conf[ty * w + tx];
            }
        c /= (2 * P + 1) * (2 * P + 1);
        for (int dy = -P; dy <= P; dy++)
            for (int dx = -P; dx <= P; dx++)
            {
                int tx = x + dx, ty = y + dy;
                if (tx < 0 || ty < 0 || tx >= w || ty >= h) continue;
                int t = ty * w + tx;
                if (!hole[t]) continue;
                img.Copy(bx + dx, by + dy, tx, ty);
                hole[t] = false;
                conf[t] = c;
                filledHere++;
            }
        left -= filledHere;
    }
}

struct Color4
{
    public float R, G, B, A;
    public Color4(float r, float g, float b, float a) { R = r; G = g; B = b; A = a; }
    public static Color4 Lerp(Color4 a, Color4 b, float t) =>
        new Color4(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
}

sealed class Img
{
    public const int Width = 1672;
    public readonly int W, H;
    public readonly float[] R, G, B, A;

    public Img(int w, int h)
    {
        W = w; H = h;
        R = new float[w * h]; G = new float[w * h]; B = new float[w * h]; A = new float[w * h];
        Array.Fill(A, 1f);
    }

    public float Lum(int i) => 0.3f * R[i] + 0.59f * G[i] + 0.11f * B[i];
    public Color4 Get(int x, int y) { int i = y * W + x; return new Color4(R[i], G[i], B[i], A[i]); }
    public void Set(int x, int y, Color4 c) { int i = y * W + x; R[i] = c.R; G[i] = c.G; B[i] = c.B; A[i] = c.A; }
    public void Copy(int fx, int fy, int tx, int ty) => Set(tx, ty, Get(fx, fy));

    public Img Clone()
    {
        var c = new Img(W, H);
        Array.Copy(R, c.R, R.Length); Array.Copy(G, c.G, G.Length); Array.Copy(B, c.B, B.Length); Array.Copy(A, c.A, A.Length);
        return c;
    }

    public Img Crop(int x0, int y0, int w, int h)
    {
        var c = new Img(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                c.Set(x, y, Get(x + x0, y + y0));
        return c;
    }

    public void MaskAlpha(Func<int, int, float> alpha)
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                A[y * W + x] *= alpha(x, y);
    }

    // Softens a hard cut-out edge by averaging alpha with the neighbours.
    public void Feather(int passes)
    {
        for (int p = 0; p < passes; p++)
        {
            var a = (float[])A.Clone();
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x;
                    A[i] = (a[i] * 4 + a[i - 1] + a[i + 1] + a[i - W] + a[i + W]) / 8f;
                }
        }
    }

    // Shrunk by `factor` and box-blurred: shown stretched, it is a soft copy.
    public Img Blurred(int factor)
    {
        int w = W / factor, h = H / factor;
        var small = new Img(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float r = 0, g = 0, b = 0;
                for (int dy = 0; dy < factor; dy++)
                    for (int dx = 0; dx < factor; dx++)
                    {
                        int i = (y * factor + dy) * W + x * factor + dx;
                        r += R[i]; g += G[i]; b += B[i];
                    }
                int n = factor * factor;
                small.Set(x, y, new Color4(r / n, g / n, b / n, 1f));
            }
        for (int pass = 0; pass < 3; pass++)
        {
            var copy = small.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float r = 0, g = 0, b = 0; int n = 0;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int qx = Math.Clamp(x + dx, 0, w - 1), qy = Math.Clamp(y + dy, 0, h - 1);
                            Color4 c = copy.Get(qx, qy);
                            r += c.R; g += c.G; b += c.B; n++;
                        }
                    small.Set(x, y, new Color4(r / n, g / n, b / n, 1f));
                }
        }
        return small;
    }

    public static Img Load(string path)
    {
        using var bmp = new Bitmap(path);
        var img = new Img(bmp.Width, bmp.Height);
        BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var bytes = new byte[data.Stride * bmp.Height];
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        bmp.UnlockBits(data);
        for (int y = 0; y < img.H; y++)
            for (int x = 0; x < img.W; x++)
            {
                int s = y * data.Stride + x * 4, i = y * img.W + x;
                img.B[i] = bytes[s] / 255f; img.G[i] = bytes[s + 1] / 255f; img.R[i] = bytes[s + 2] / 255f; img.A[i] = bytes[s + 3] / 255f;
            }
        return img;
    }

    public void Save(string path)
    {
        using var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        BitmapData data = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var bytes = new byte[data.Stride * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int s = y * data.Stride + x * 4, i = y * W + x;
                bytes[s] = To8(B[i]); bytes[s + 1] = To8(G[i]); bytes[s + 2] = To8(R[i]); bytes[s + 3] = To8(A[i]);
            }
        Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        bmp.UnlockBits(data);
        bmp.Save(path, ImageFormat.Png);
    }

    private static byte To8(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
}

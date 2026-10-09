// Makes the quiet twins of the golden buttons: the same chamfered plate with its diamonds, the caption smoothed
// away and the bright gold dimmed to unlit bronze. The game writes the caption over them.
// Sources are the golden buttons cut from the painted screens into ArtSource/UI (tools/cut_settings.py, cut_menu.py).
// - UI/settings_cancel.png from settings_done.png: "ОТМЕНА" beside "ГОТОВО" (SettingsScreen.cs).
// - UI/menu_button.png from menu_play.png: "НАСТРОЙКИ" and "ВЫХОД" under "ИГРАТЬ" (GameMenu.cs).
// - UI/menu_play_blank.png: "ИГРАТЬ" itself with its caption cleared but its gold kept, so all three captions are
//   written alike.
// - UI/map_button.png from UI/map_play.png (already blank, cut by tools/cut_level_map.cs): "НАЗАД" and "НАЧАТЬ
//   ЗАНОВО" on the level map, stretched in the middle only (its .meta keeps 72 px at each end).
// - UI/settings_done_blank.png: "ГОТОВО" the same way, its caption written in code like "ОТМЕНА".
// Run from the repository root (needs the .NET 10 SDK):  dotnet run tools/unlit_buttons.cs
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#pragma warning disable CA1416

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Source, target and the box of the caption to clear.
Unlit("ArtSource/UI/settings_done.png", "Assets/Resources/UI/settings_cancel.png", 124, 320, 27, 84, true);
Unlit("ArtSource/UI/settings_done.png", "Assets/Resources/UI/settings_done_blank.png", 124, 320, 27, 84, false);
Unlit("ArtSource/UI/menu_play.png", "Assets/Resources/UI/menu_button.png", 90, 344, 26, 97, true);
Unlit("ArtSource/UI/menu_play.png", "Assets/Resources/UI/menu_play_blank.png", 90, 344, 26, 97, false);
Unlit("Assets/Resources/UI/map_play.png", "Assets/Resources/UI/map_button.png", 1, 0, 1, 0, true);

static void Unlit(string source, string target, int x0, int x1, int y0, int y1, bool dim)
{
    using var bmp = new Bitmap(source);
    int w = bmp.Width, h = bmp.Height;
    BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
    var bytes = new byte[data.Stride * h];
    Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
    var r = new float[w * h]; var g = new float[w * h]; var b = new float[w * h];
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int s = y * data.Stride + x * 4, i = y * w + x;
            b[i] = bytes[s]; g[i] = bytes[s + 1]; r[i] = bytes[s + 2];
        }

    // The caption is filled in from the edges of its box (none when x1 < x0): rows blended end to end, then relaxed.
    for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float t = (x - x0 + 1f) / (x1 - x0 + 2f);
            int i = y * w + x, a = y * w + x0 - 1, z = y * w + x1 + 1;
            r[i] = r[a] + (r[z] - r[a]) * t; g[i] = g[a] + (g[z] - g[a]) * t; b[i] = b[a] + (b[z] - b[a]) * t;
        }
    for (int pass = 0; pass < 3000 && x1 >= x0; pass++)
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * w + x;
                r[i] = (r[i - 1] + r[i + 1] + r[i - w] + r[i + w]) * 0.25f;
                g[i] = (g[i - 1] + g[i + 1] + g[i - w] + g[i + w]) * 0.25f;
                b[i] = (b[i - 1] + b[i + 1] + b[i - w] + b[i + w]) * 0.25f;
            }

    // Unlit: half the colour drained towards grey, then darkened, the bevels keeping some of their warmth.
    for (int i = 0; i < w * h && dim; i++)
    {
        float l = 0.3f * r[i] + 0.59f * g[i] + 0.11f * b[i];
        r[i] = (r[i] + (l - r[i]) * 0.5f) * 0.42f;
        g[i] = (g[i] + (l - g[i]) * 0.5f) * 0.4f;
        b[i] = (b[i] + (l - b[i]) * 0.5f) * 0.36f;
    }

    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int s = y * data.Stride + x * 4, i = y * w + x;
            bytes[s] = (byte)Math.Clamp((int)b[i], 0, 255);
            bytes[s + 1] = (byte)Math.Clamp((int)g[i], 0, 255);
            bytes[s + 2] = (byte)Math.Clamp((int)r[i], 0, 255);
        }
    Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
    bmp.UnlockBits(data);
    bmp.Save(target, ImageFormat.Png);
    Console.WriteLine(target + " written");
}

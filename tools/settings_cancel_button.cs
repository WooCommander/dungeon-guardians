// Makes UI/settings_cancel.png from UI/settings_done.png: the same chamfered plate with its diamonds, the caption
// "ГОТОВО" smoothed away and the bright gold dimmed to unlit bronze, so "ОТМЕНА" (drawn by SettingsScreen.cs) sits
// beside "ГОТОВО" as its quiet twin.
// Run from the repository root (needs the .NET 10 SDK):  dotnet run tools/settings_cancel_button.cs
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#pragma warning disable CA1416

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

const string Source = "Assets/Resources/UI/settings_done.png";
const string Target = "Assets/Resources/UI/settings_cancel.png";

using var bmp = new Bitmap(Source);
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

// The caption (130..313 x 32..78) is filled in from its edges: rows blended end to end, then relaxed.
const int X0 = 124, X1 = 320, Y0 = 27, Y1 = 84;
for (int y = Y0; y <= Y1; y++)
    for (int x = X0; x <= X1; x++)
    {
        float t = (x - X0 + 1f) / (X1 - X0 + 2f);
        int i = y * w + x, a = y * w + X0 - 1, z = y * w + X1 + 1;
        r[i] = r[a] + (r[z] - r[a]) * t; g[i] = g[a] + (g[z] - g[a]) * t; b[i] = b[a] + (b[z] - b[a]) * t;
    }
for (int pass = 0; pass < 2000; pass++)
    for (int y = Y0; y <= Y1; y++)
        for (int x = X0; x <= X1; x++)
        {
            int i = y * w + x;
            r[i] = (r[i - 1] + r[i + 1] + r[i - w] + r[i + w]) * 0.25f;
            g[i] = (g[i - 1] + g[i + 1] + g[i - w] + g[i + w]) * 0.25f;
            b[i] = (b[i - 1] + b[i + 1] + b[i - w] + b[i + w]) * 0.25f;
        }

// Unlit: half the colour drained towards grey, then darkened, the bevels keeping some of their warmth.
for (int i = 0; i < w * h; i++)
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
bmp.Save(Target, ImageFormat.Png);
Console.WriteLine("settings_cancel.png written");

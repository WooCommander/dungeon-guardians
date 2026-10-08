// Makes the app icons from the guardian statue of the start screen (map-images/menu.png): its face with the
// glowing eyes and the crystal on its brow, in Assets/Art/Icons/ (BuildTool.cs puts them in the player settings).
// - icon.png: a rounded square in a gold frame (Windows, and Android's legacy icon).
// - icon_round.png: the same in a circle (Android's round icon).
// - icon_adaptive_background.png, icon_adaptive_foreground.png: Android's adaptive icon, the picture full-bleed
//   (taken wider, so the face stays inside the part every launcher's mask keeps) under an empty layer.
// Run from the repository root (needs the .NET 10 SDK):  dotnet run tools/make_icons.cs
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#pragma warning disable CA1416

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

const int Size = 1024;
const string Output = "Assets/Art/Icons/";
Directory.CreateDirectory(Output);
using var menu = new Bitmap("map-images/menu.png");

// The face fills the framed icons; the adaptive one shows more around it.
var face = new Rectangle(1228, 52, 400, 400);
var wide = new Rectangle(1152, 0, 520, 520);

Framed(face, rounded: true).Save(Output + "icon.png", ImageFormat.Png);
Framed(face, rounded: false).Save(Output + "icon_round.png", ImageFormat.Png);
Picture(wide).Save(Output + "icon_adaptive_background.png", ImageFormat.Png);
new Bitmap(Size, Size, PixelFormat.Format32bppArgb).Save(Output + "icon_adaptive_foreground.png", ImageFormat.Png);
Console.WriteLine("icons written to " + Output);

// A crop of the menu picture scaled to the icon, darkened towards the edges so the face stands out.
Bitmap Picture(Rectangle crop)
{
    var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
    using Graphics g = Graphics.FromImage(bmp);
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.DrawImage(menu, new Rectangle(0, 0, Size, Size), crop, GraphicsUnit.Pixel);
    using var vignette = new GraphicsPath();
    vignette.AddEllipse(-Size * 0.25f, -Size * 0.25f, Size * 1.5f, Size * 1.5f);
    using var shade = new PathGradientBrush(vignette)
    {
        CenterColor = Color.FromArgb(0, 0, 0, 0),
        SurroundColors = new[] { Color.FromArgb(170, 0, 0, 0) },
        FocusScales = new PointF(0.55f, 0.55f),
    };
    g.FillRectangle(shade, 0, 0, Size, Size);
    return bmp;
}

// The picture cut to a rounded square or a circle, in a gold frame, on transparency.
Bitmap Framed(Rectangle crop, bool rounded)
{
    using Bitmap picture = Picture(crop);
    var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
    using Graphics g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    const float Margin = 20f, Frame = 34f;
    RectangleF outer = new RectangleF(Margin, Margin, Size - 2 * Margin, Size - 2 * Margin);
    using GraphicsPath shape = rounded ? RoundedSquare(outer, Size * 0.2f) : Circle(outer);
    using (var brush = new TextureBrush(picture))
    {
        g.FillPath(brush, shape);
    }

    // The frame: a dark rim, a band of gold shading from light at the top to deep at the bottom, a bright edge.
    using (var rim = new Pen(Color.FromArgb(255, 30, 16, 6), Frame + 10f))
    {
        g.DrawPath(rim, shape);
    }

    using (var gold = new LinearGradientBrush(new PointF(0, 0), new PointF(0, Size),
        Color.FromArgb(255, 255, 222, 140), Color.FromArgb(255, 150, 82, 22)))
    using (var band = new Pen(gold, Frame))
    {
        g.DrawPath(band, shape);
    }

    using (var edge = new Pen(Color.FromArgb(200, 255, 240, 190), 4f))
    {
        RectangleF inner = RectangleF.Inflate(outer, -Frame / 2f + 2f, -Frame / 2f + 2f);
        using GraphicsPath innerShape = rounded ? RoundedSquare(inner, Size * 0.2f - Frame / 2f) : Circle(inner);
        g.DrawPath(edge, innerShape);
    }

    return bmp;
}

static GraphicsPath RoundedSquare(RectangleF r, float radius)
{
    var path = new GraphicsPath();
    float d = radius * 2f;
    path.AddArc(r.X, r.Y, d, d, 180, 90);
    path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}

static GraphicsPath Circle(RectangleF r)
{
    var path = new GraphicsPath();
    path.AddEllipse(r);
    return path;
}

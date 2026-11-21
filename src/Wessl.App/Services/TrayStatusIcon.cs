using Microsoft.UI.Reactor;

namespace Wessl.App.Services;

internal static class TrayStatusIcon
{
    private const int Size = 32;
    private const int SamplesPerAxis = 4;

    public static WindowIcon Create(bool running)
    {
        var pixels = new byte[Size * Size * 4];
        var indicator = running
            ? new Rgba(77, 201, 125, 255)
            : new Rgba(147, 158, 168, 255);

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var red = 0;
                var green = 0;
                var blue = 0;
                var alpha = 0;

                for (var sampleY = 0; sampleY < SamplesPerAxis; sampleY++)
                {
                    for (var sampleX = 0; sampleX < SamplesPerAxis; sampleX++)
                    {
                        var pointX = x + (sampleX + 0.5) / SamplesPerAxis;
                        var pointY = y + (sampleY + 0.5) / SamplesPerAxis;
                        var color = GetColor(pointX, pointY, indicator);
                        red += color.Red;
                        green += color.Green;
                        blue += color.Blue;
                        alpha += color.Alpha;
                    }
                }

                var offset = (y * Size + x) * 4;
                var sampleCount = SamplesPerAxis * SamplesPerAxis;
                pixels[offset] = (byte)(red / sampleCount);
                pixels[offset + 1] = (byte)(green / sampleCount);
                pixels[offset + 2] = (byte)(blue / sampleCount);
                pixels[offset + 3] = (byte)(alpha / sampleCount);
            }
        }

        return WindowIcon.FromRgba(pixels, Size, Size);
    }

    private static Rgba GetColor(double x, double y, Rgba indicator)
    {
        if (!IsInsideRoundedRectangle(x, y, 16, 16, 14, 6))
        {
            return default;
        }

        var color = new Rgba(48, 73, 91, 255);
        if (IsNearLine(x, y, 8.5, 8, 11.5, 23, 1.4)
            || IsNearLine(x, y, 11.5, 23, 16, 14, 1.4)
            || IsNearLine(x, y, 16, 14, 20.5, 23, 1.4)
            || IsNearLine(x, y, 20.5, 23, 23.5, 8, 1.4))
        {
            color = new Rgba(245, 248, 250, 255);
        }

        var distanceToIndicator = Distance(x, y, 24, 23);
        if (distanceToIndicator <= 4.8)
        {
            color = new Rgba(28, 43, 55, 255);
        }

        if (distanceToIndicator <= 3.6)
        {
            color = indicator;
        }

        return color;
    }

    private static bool IsInsideRoundedRectangle(double x, double y, double centerX, double centerY, double halfSize, double radius)
    {
        var cornerX = Math.Abs(x - centerX) - (halfSize - radius);
        var cornerY = Math.Abs(y - centerY) - (halfSize - radius);
        var outsideDistance = Math.Sqrt(
            Math.Pow(Math.Max(cornerX, 0), 2) + Math.Pow(Math.Max(cornerY, 0), 2));
        var insideDistance = Math.Min(Math.Max(cornerX, cornerY), 0);
        return outsideDistance + insideDistance <= radius;
    }

    private static bool IsNearLine(double x, double y, double startX, double startY, double endX, double endY, double radius)
    {
        var lineX = endX - startX;
        var lineY = endY - startY;
        var position = Math.Clamp(((x - startX) * lineX + (y - startY) * lineY)
            / (lineX * lineX + lineY * lineY), 0, 1);
        return Distance(x, y, startX + position * lineX, startY + position * lineY) <= radius;
    }

    private static double Distance(double x, double y, double targetX, double targetY) =>
        Math.Sqrt(Math.Pow(x - targetX, 2) + Math.Pow(y - targetY, 2));

    private readonly record struct Rgba(byte Red, byte Green, byte Blue, byte Alpha);
}

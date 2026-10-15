using System.Diagnostics;
using System.Numerics;
using System.Text;
using HybridSdfEngine.Render;

RayTracingChecks.Run();
SceneChecks.Run();
MaterialChecks.Run();
LightChecks.Run();
ExtendedLightChecks.Run();
ShadowChecks.Run();
ConsoleDisplayStateChecks.Run();

using var output = new MemoryStream();
using var display = new ConsoleDisplay(output);
var screen = new Screen(24, 8);
string Capture()
{
    display.Render();
    string text = Encoding.UTF8.GetString(output.ToArray());
    output.SetLength(0);
    screen.Apply(text);
    return text;
}
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
void Verify(Vector3[] frame, int width, int height)
{
    static int Channel(float v) => v > 0 ? (int)MathF.Min(v * 255, 255) : 0;
    for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var p = frame[y * width + x];
            int color = Channel(p.X) << 16 | Channel(p.Y) << 8 | Channel(p.Z);
            Check(screen.Colors[y, x * 2] == color && screen.Colors[y, x * 2 + 1] == color,
                $"Wrong pixel at {x},{y}");
        }
}

var random = new Random(123);
var frame = new Vector3[12 * 8];
for (int iteration = 0; iteration < 100; iteration++)
{
    for (int i = 0; i < frame.Length; i++)
        if (random.Next(4) == 0) frame[i] = new Vector3(random.NextSingle(), random.NextSingle(), random.NextSingle());
    display.ColoredGraphic(12, 8, frame);
    Capture();
    Verify(frame, 12, 8);
    display.ColoredGraphic(12, 8, frame);
    Check(Capture().Length == 0, "Identical frame must emit nothing");
}
display.ColoredGraphic(12, 8, new Vector3[96]); // Prepared but never displayed.
display.ColoredGraphic(12, 8, frame);
Check(Capture().Length == 0, "Compare with displayed frame, not prepared frame");
display.Invalidate();
display.ColoredGraphic(12, 8, frame);
Check(Capture().Length > 0, "Invalidation must repaint");
Verify(frame, 12, 8);
display.MaskGraphics(5, 1, [0, 0.2f, 0.4f, 0.6f, 0.8f]);
Check(Capture().EndsWith(" ░▒▓█"), "Mask encoding or thresholds");
display.ColoredGraphic(12, 8, frame);
Check(Capture().Length > 0, "Mask must invalidate color cache");
Verify(frame, 12, 8);
Vector3[] limits = [new(float.NaN, float.NegativeInfinity, float.PositiveInfinity), new(-1, 0.5f, 2)];
display.ColoredGraphic(2, 1, limits);
Capture();
Verify(limits, 2, 1);
try
{
    display.ColoredGraphic(3, 1, limits);
    throw new Exception("Short input accepted");
}
catch (ArgumentException) { }
Console.WriteLine("PASS: random partial updates, unchanged frames, pending frames, invalidation, mask, resize, channel limits, validation.");

// Compare preparation + stream write, without terminal rasterization or ray marching.
const int width = 160, height = 90, iterations = 500;
var first = new Vector3[width * height];
var second = new Vector3[first.Length];
for (int i = 0; i < first.Length; i++)
{
    first[i] = new(random.NextSingle(), random.NextSingle(), random.NextSingle());
    second[i] = new(random.NextSingle(), random.NextSingle(), random.NextSingle());
}
using var optimized = new ConsoleDisplay(Stream.Null);
void Old(Vector3[] pixels)
{
    var builder = new StringBuilder();
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            var p = pixels[y * width + x];
            int r = (int)Math.Clamp(p.X * 255, 0, 255);
            int g = (int)Math.Clamp(p.Y * 255, 0, 255);
            int b = (int)Math.Clamp(p.Z * 255, 0, 255);
            builder.Append($"\u001b[48;2;{r};{g};{b}m  ");
        }
        builder.Append('\n');
    }
    Stream.Null.Write(Encoding.UTF8.GetBytes(builder.ToString()));
}
void Measure(string name, Action<int> action)
{
    for (int i = 0; i < 100; i++) action(i);
    GC.Collect();
    long before = GC.GetAllocatedBytesForCurrentThread();
    var timer = Stopwatch.StartNew();
    for (int i = 0; i < iterations; i++) action(i);
    timer.Stop();
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Console.WriteLine($"{name}: {timer.Elapsed.TotalMilliseconds / iterations:F3} ms/frame, {allocated / iterations} bytes allocated/frame");
}
Measure("Original, changing random frame", i => Old(i % 2 == 0 ? first : second));
Measure("Optimized, changing random frame", i =>
{
    optimized.ColoredGraphic(width, height, i % 2 == 0 ? first : second);
    optimized.Render();
});
Measure("Optimized, unchanged frame", _ =>
{
    optimized.ColoredGraphic(width, height, first);
    optimized.Render();
});
Measure("Optimized, one changing pixel", i =>
{
    first[0] = i % 2 == 0 ? Vector3.Zero : Vector3.One;
    optimized.ColoredGraphic(width, height, first);
    optimized.Render();
});
Array.Fill(first, new Vector3(0.2f, 0.4f, 0.6f));
Array.Fill(second, new Vector3(0.8f, 0.6f, 0.4f));
Measure("Original, changing solid frame", i => Old(i % 2 == 0 ? first : second));
Measure("Optimized, changing solid frame", i =>
{
    optimized.ColoredGraphic(width, height, i % 2 == 0 ? first : second);
    optimized.Render();
});

sealed class Screen(int width, int height)
{
    public int[,] Colors { get; } = new int[height, width];
    private int _row, _column, _color;
    public void Apply(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\u001b')
            {
                int start = i + 2;
                i = start;
                while (!char.IsLetter(text[i])) i++;
                int[] args = text[start..i].Split(';').Select(int.Parse).ToArray();
                if (text[i] == 'H') { _row = args[0] - 1; _column = args[1] - 1; }
                else if (text[i] == 'm')
                    _color = args[0] == 0 ? 0 : args[2] << 16 | args[3] << 8 | args[4];
                else throw new Exception("Unexpected ANSI command");
            }
            else
            {
                if (_column >= width) throw new Exception("Unexpected row wrapping");
                Colors[_row, _column++] = _color;
            }
        }
    }
}

using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;

namespace StringSpectrum.Tests;

[Collection("Direct2D")]
public sealed class StringSpectrumRenderingTests
{
    const float Width = 200f;
    const float Amplitude = 20f;
    const float Thickness = 3f;
    const int HalfCrop = 101;
    const int HalfHeight = 40;

    static readonly Vector4 Crop = new(-HalfCrop, -HalfHeight, HalfCrop, HalfHeight);
    static readonly Bgra White = Bgra.Opaque(255, 255, 255);

    static byte[] Weights(params float[] weights)
    {
        var modes = new float[StringSpectrumCustomEffect.MaxModes];
        weights.CopyTo(modes, 0);
        return MemoryMarshal.AsBytes(modes.AsSpan()).ToArray();
    }

    static void Configure(StringSpectrumCustomEffect effect, int modeCount, byte[] modes, float thickness = Thickness, float width = Width, float amplitude = Amplitude)
    {
        effect.Width = width;
        effect.Amplitude = amplitude;
        effect.ModeCount = modeCount;
        effect.Thickness = thickness;
        effect.ColorR = 1f;
        effect.ColorG = 1f;
        effect.ColorB = 1f;
        effect.ColorA = 1f;
        effect.Modes = modes;
    }

    static Rendering Render(IGraphicsDevicesAndContext devices, Action<StringSpectrumCustomEffect> configure)
    {
        var context = devices.DeviceContext;
        using var flood = new Flood(context) { Color = Vector4.Zero };
        using var effect = new StringSpectrumCustomEffect(devices);
        using var clip = new Crop(context) { Rectangle = Crop };
        using var floodOutput = flood.Output;
        effect.SetInput(0, floodOutput, true);
        configure(effect);
        using var effectOutput = effect.Output;
        clip.SetInput(0, effectOutput, true);
        using var output = clip.Output;
        return Rendering.Capture(devices, output);
    }

    static Rendering AtRest(IGraphicsDevicesAndContext devices, float thickness = Thickness, float width = Width)
        => Render(devices, effect => Configure(effect, 1, Weights(), thickness, width));

    static int OpaqueRows(Rendering rendering, int x) => rendering.Rows().Count(y => rendering[x, y].Alpha == byte.MaxValue);

    static bool WithinRounding(Bgra expected, Bgra actual)
        => Math.Abs(expected.Blue - actual.Blue) <= 1 && Math.Abs(expected.Green - actual.Green) <= 1 && Math.Abs(expected.Red - actual.Red) <= 1 && Math.Abs(expected.Alpha - actual.Alpha) <= 1;

    [Fact]
    public void TheRenderingCoversTheCrop()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = AtRest(context);

        Assert.Equal((-HalfCrop, -HalfHeight, 2 * HalfCrop, 2 * HalfHeight), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
    }

    [Fact]
    public void WithoutModesNothingIsDrawn()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = Render(context, effect => Configure(effect, 0, Weights(1f)));

        Assert.All(rendering.Coordinates(), point => Assert.Equal(Bgra.Transparent, rendering[point.X, point.Y]));
    }

    [Fact]
    public void AtRestTheStringIsAHorizontalLineAlongItsLength()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = AtRest(context);

        foreach (var x in Enumerable.Range(-100, 200))
        {
            Assert.Equal(White, rendering[x, -1]);
            Assert.Equal(White, rendering[x, 0]);
            foreach (var y in rendering.Rows().Where(y => y <= -3 || y >= 2))
                Assert.Equal(Bgra.Transparent, rendering[x, y]);
        }
    }

    [Fact]
    public void TheStringEndsExactlyAtItsLength()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = AtRest(context);

        Assert.Equal(White, rendering[-100, -1]);
        Assert.Equal(White, rendering[99, -1]);
        Assert.All(rendering.Rows(), y => Assert.Equal(Bgra.Transparent, rendering[-101, y]));
        Assert.All(rendering.Rows(), y => Assert.Equal(Bgra.Transparent, rendering[100, y]));
    }

    [Fact]
    public void AShorterStringSpansFewerColumns()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = AtRest(context, width: 100f);

        Assert.Equal(White, rendering[-50, -1]);
        Assert.Equal(White, rendering[49, -1]);
        Assert.All(rendering.Rows(), y => Assert.Equal(Bgra.Transparent, rendering[-51, y]));
        Assert.All(rendering.Rows(), y => Assert.Equal(Bgra.Transparent, rendering[50, y]));
    }

    [Theory]
    [InlineData(1f, -21, -20, 20)]
    [InlineData(-1f, 20, 19, -21)]
    public void TheFirstModeSwingsTheCentreByTheAmplitude(float weight, int outerRow, int innerRow, int oppositeRow)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = Render(context, effect => Configure(effect, 1, Weights(weight)));

        Assert.Equal(White, rendering[0, outerRow]);
        Assert.Equal(White, rendering[0, innerRow]);
        Assert.Equal(Bgra.Transparent, rendering[0, -1]);
        Assert.Equal(Bgra.Transparent, rendering[0, 0]);
        Assert.Equal(Bgra.Transparent, rendering[0, oppositeRow]);
    }

    [Fact]
    public void TheEndsStayFixedWhileTheCentreSwings()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = Render(context, effect => Configure(effect, 1, Weights(1f)));

        Assert.Equal(White, rendering[0, -21]);
        Assert.Equal(White, rendering[-100, -1]);
        Assert.Equal(White, rendering[99, -1]);
        foreach (var y in rendering.Rows().Where(y => y <= -4 || y >= 3))
        {
            Assert.Equal(Bgra.Transparent, rendering[-100, y]);
            Assert.Equal(Bgra.Transparent, rendering[99, y]);
        }
    }

    [Fact]
    public void TheFirstModeIsSymmetricAboutTheCentre()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = Render(context, effect => Configure(effect, 1, Weights(1f)));

        Assert.Contains(rendering.Coordinates(), point => rendering[point.X, point.Y].Alpha > 0);
        Assert.All(rendering.Coordinates(), point => Assert.True(WithinRounding(rendering[point.X, point.Y], rendering[-1 - point.X, point.Y]), $"({point.X}, {point.Y})"));
    }

    [Fact]
    public void TheSecondModeIsAntisymmetricAboutTheCentre()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = Render(context, effect => Configure(effect, 2, Weights(0f, 1f)));

        Assert.Contains(rendering.Coordinates(), point => rendering[point.X, point.Y].Alpha > 0 && point.Y < -5);
        Assert.All(rendering.Coordinates(), point => Assert.True(WithinRounding(rendering[point.X, point.Y], rendering[-1 - point.X, -1 - point.Y]), $"({point.X}, {point.Y})"));
    }

    [Theory]
    [InlineData(1f, 0)]
    [InlineData(2f, 0)]
    [InlineData(3f, 2)]
    [InlineData(4f, 2)]
    [InlineData(5f, 4)]
    [InlineData(7f, 6)]
    [InlineData(11f, 10)]
    public void TheThicknessSetsTheFullyCoveredRowsWithAOnePixelFringe(float thickness, int opaqueRows)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = AtRest(context, thickness);

        Assert.Equal(opaqueRows, OpaqueRows(rendering, 0));
        Assert.True(rendering[0, -1].Alpha > 0);
        Assert.True(rendering[0, 0].Alpha > 0);
    }

    [Fact]
    public void ASteeperStringCoversMoreRowsWithTheSameThickness()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rest = AtRest(context, 11f);
        var steep = Render(context, effect => Configure(effect, 2, Weights(0f, 1f), 11f));

        Assert.Equal(10, OpaqueRows(rest, 0));
        Assert.Equal(11, OpaqueRows(steep, 0));
    }

    [Fact]
    public void TheTintIsAppliedPremultiplied()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = Render(context, effect =>
        {
            Configure(effect, 1, Weights());
            effect.ColorR = 0.2f;
            effect.ColorG = 0.4f;
            effect.ColorB = 0.8f;
            effect.ColorA = 0.6f;
        });

        Assert.Equal(new Bgra(122, 61, 31, 153), rendering[0, -1]);
        Assert.Equal(Bgra.Transparent, rendering[0, -3]);
    }

    [Fact]
    public void TheModesAreReadInBlocksOfFour()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var fifthModeOnly = Weights(0f, 0f, 0f, 0f, 1f);

        var four = Render(context, effect => Configure(effect, 4, fifthModeOnly));
        var five = Render(context, effect => Configure(effect, 5, fifthModeOnly));

        Assert.Equal(White, four[0, -1]);
        Assert.Equal(Bgra.Transparent, four[0, -21]);
        Assert.Equal(Bgra.Transparent, five[0, -1]);
        Assert.Equal(White, five[0, -21]);
    }

    [Fact]
    public void TheLastModeIsReadWithTheLastBlock()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var lastModeOnly = Weights(Enumerable.Range(0, StringSpectrumCustomEffect.MaxModes).Select(index => index == StringSpectrumCustomEffect.MaxModes - 1 ? 1f : 0f).ToArray());

        var rest = AtRest(context);
        var sixty = Render(context, effect => Configure(effect, 60, lastModeOnly));
        var sixtyOne = Render(context, effect => Configure(effect, 61, lastModeOnly));

        Assert.True(sixty.SamePixelsAs(rest));
        Assert.False(sixtyOne.SamePixelsAs(rest));
    }

    [Fact]
    public void TheThirdModeHasNodesAtThirdsOfTheString()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = Render(context, effect => Configure(effect, 3, Weights(0f, 0f, 1f)));

        Assert.Equal(White, rendering[-67, -21]);
        Assert.Equal(White, rendering[-67, -20]);
        Assert.Equal(White, rendering[-34, -1]);
        Assert.Equal(White, rendering[-34, 0]);
        Assert.Equal(White, rendering[0, 19]);
        Assert.Equal(White, rendering[0, 20]);
        Assert.Equal(Bgra.Transparent, rendering[0, -1]);
        Assert.Equal(White, rendering[33, -1]);
        Assert.Equal(White, rendering[33, 0]);
        Assert.Equal(White, rendering[66, -21]);
        Assert.Equal(White, rendering[66, -20]);
    }

    [Fact]
    public void AFullyTransparentTintDrawsNothing()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = Render(context, effect =>
        {
            Configure(effect, 1, Weights(1f));
            effect.ColorA = 0f;
        });

        Assert.All(rendering.Coordinates(), point => Assert.Equal(Bgra.Transparent, rendering[point.X, point.Y]));
    }

    [Fact]
    public void TheSameSettingsAlwaysProduceTheSamePixels()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        void Swinging(StringSpectrumCustomEffect effect) => Configure(effect, 3, Weights(0.5f, -0.25f, 0.125f), 5f);

        var first = Render(context, Swinging);
        var second = Render(context, Swinging);

        Assert.True(first.SamePixelsAs(second));
    }
}

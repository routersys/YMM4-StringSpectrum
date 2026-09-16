using System.Globalization;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Plugin.Shape;

namespace StringSpectrum.Tests;

[Collection("Direct2D")]
public sealed class StringSpectrumSourceTests
{
    const int Length = 60;
    const double Width = 200d;
    const double Amplitude = 20d;
    const double Thickness = 3d;

    static readonly Bgra White = Bgra.Opaque(255, 255, 255);
    static readonly float[] Silent = new float[32];
    static readonly float[] Loud = Enumerable.Repeat(1f, StringSpectrumCustomEffect.MaxModes).ToArray();
    static readonly float[] HighestBandOnly = Enumerable.Range(0, StringSpectrumCustomEffect.MaxModes).Select(index => index == StringSpectrumCustomEffect.MaxModes - 1 ? 1f : 0f).ToArray();
    static readonly float[] Alternating = Enumerable.Range(0, 32).Select(index => index % 2 == 0 ? 1f : -1f).ToArray();

    static StringSpectrumParameter Parameter(double width = Width, double amplitude = Amplitude, double thickness = Thickness, int modeLimit = 24, double baseFrequency = 0d)
    {
        var parameter = new StringSpectrumParameter { ModeLimit = modeLimit };
        parameter.StringWidth.Values[0].Value = width;
        parameter.Amplitude.Values[0].Value = amplitude;
        parameter.Thickness.Values[0].Value = thickness;
        parameter.BaseFrequency.Values[0].Value = baseFrequency;
        return parameter;
    }

    static Rendering RenderFrame(IGraphicsDevicesAndContext devices, IAudioSpectrumSource source, int frame, float[] spectrum)
    {
        source.Update(ItemDescriptions.At(frame, Length), spectrum);
        return Rendering.Capture(devices, source.Output);
    }

    static int OpaqueRows(Rendering rendering, int x) => rendering.Rows().Count(y => rendering[x, y].Alpha == byte.MaxValue);

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}"""))!;

    static bool WithinRounding(Bgra expected, Bgra actual)
        => Math.Abs(expected.Blue - actual.Blue) <= 1 && Math.Abs(expected.Green - actual.Green) <= 1 && Math.Abs(expected.Red - actual.Red) <= 1 && Math.Abs(expected.Alpha - actual.Alpha) <= 1;

    [Theory]
    [InlineData(600d, -301)]
    [InlineData(200d, -101)]
    public void TheOutputIsCroppedToTheLengthOfTheStringPlusOnePixel(double width, int left)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter(width).CreateShapeSource(context);

        var rendering = RenderFrame(context, source, 0, Silent);

        Assert.Equal(left, rendering.Left);
        Assert.Equal(-left, rendering.Right);
    }

    [Fact]
    public void TheCropIsSymmetricAboutTheOrigin()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter().CreateShapeSource(context);

        var rendering = RenderFrame(context, source, 0, Loud);

        Assert.Equal(-rendering.Left, rendering.Right);
        Assert.Equal(-rendering.Top, rendering.Bottom);
    }

    [Fact]
    public void TheCropGrowsWithTheSuperposedModes()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter().CreateShapeSource(context);

        var single = RenderFrame(context, source, 0, [0f]);
        var many = RenderFrame(context, source, 0, Silent);

        Assert.True(many.Bottom > single.Bottom, $"{single.Bottom} -> {many.Bottom}");
    }

    public static readonly TheoryData<string, double, double, double, int, float[], int> Swings = new()
    {
        { "loud", 600d, 120d, 3d, StringSpectrumCustomEffect.MaxModes, Loud, 0 },
        { "loud later", 600d, 120d, 3d, StringSpectrumCustomEffect.MaxModes, Loud, 7 },
        { "highest band", 600d, 400d, 30d, StringSpectrumCustomEffect.MaxModes, HighestBandOnly, 13 },
        { "alternating", 60d, 200d, 1d, StringSpectrumCustomEffect.MaxModes, Alternating, 3 },
        { "single band", 200d, 20d, 3d, 24, [1f], 0 },
    };

    [Theory]
    [MemberData(nameof(Swings))]
    public void NoDrawnPixelTouchesTheCrop(string name, double width, double amplitude, double thickness, int modeLimit, float[] spectrum, int frame)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter(width, amplitude, thickness, modeLimit, 1.2d).CreateShapeSource(context);

        var rendering = RenderFrame(context, source, frame, spectrum);

        Assert.True(rendering.Coordinates().Any(point => rendering[point.X, point.Y].Alpha > 0), name);
        foreach (var x in rendering.Columns())
        {
            Assert.True(rendering[x, rendering.Top] == Bgra.Transparent, $"{name} ({x}, {rendering.Top})");
            Assert.True(rendering[x, rendering.Bottom - 1] == Bgra.Transparent, $"{name} ({x}, {rendering.Bottom - 1})");
        }

        foreach (var y in rendering.Rows())
        {
            Assert.True(rendering[rendering.Left, y] == Bgra.Transparent, $"{name} ({rendering.Left}, {y})");
            Assert.True(rendering[rendering.Right - 1, y] == Bgra.Transparent, $"{name} ({rendering.Right - 1}, {y})");
        }
    }

    [Fact]
    public void WithoutSpectrumBandsNothingIsDrawn()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter().CreateShapeSource(context);

        var rendering = RenderFrame(context, source, 0, []);

        Assert.All(rendering.Coordinates(), point => Assert.Equal(Bgra.Transparent, rendering[point.X, point.Y]));
    }

    [Fact]
    public void BandsBeyondTheCapacityAreFolded()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var folded = Enumerable.Range(0, StringSpectrumCustomEffect.MaxModes).Select(index => (index % 7) / 7f).ToArray();
        var doubled = folded.SelectMany(value => new[] { value, value }).ToArray();
        using var source = Parameter(modeLimit: StringSpectrumCustomEffect.MaxModes).CreateShapeSource(context);

        var direct = RenderFrame(context, source, 3, folded);
        var paired = RenderFrame(context, source, 3, doubled);

        Assert.True(direct.SamePixelsAs(paired));
    }

    [Fact]
    public void BandsThatAreNotFiniteCountAsSilence()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter().CreateShapeSource(context);

        var silent = RenderFrame(context, source, 0, [0f, 1f]);
        var broken = RenderFrame(context, source, 0, [float.NaN, 1f]);

        Assert.True(silent.SamePixelsAs(broken));
    }

    [Fact]
    public void WithoutASpectrumNothingIsDrawn()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter().CreateShapeSource(context);

        var rendering = RenderFrame(context, source, 0, null!);

        Assert.All(rendering.Coordinates(), point => Assert.Equal(Bgra.Transparent, rendering[point.X, point.Y]));
    }

    [Fact]
    public void BandsBeyondTheUnitRangeAreClamped()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter().CreateShapeSource(context);

        var unit = RenderFrame(context, source, 0, [1f, -1f]);
        var beyond = RenderFrame(context, source, 0, [5f, -3f]);

        Assert.True(unit.SamePixelsAs(beyond));
    }

    [Fact]
    public void ASilentSpectrumDrawsTheStringAtRest()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter().CreateShapeSource(context);

        var rendering = RenderFrame(context, source, 0, Silent);

        Assert.Equal(White, rendering[0, -1]);
        Assert.Equal(White, rendering[0, 0]);
        Assert.Equal(White, rendering[-100, -1]);
        Assert.Equal(White, rendering[99, 0]);
        foreach (var y in rendering.Rows().Where(y => y <= -3 || y >= 2))
            Assert.Equal(Bgra.Transparent, rendering[0, y]);
    }

    [Fact]
    public void TheColorReachesTheString()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var parameter = Parameter();
        parameter.StringColor = Color.FromArgb(153, 51, 102, 204);
        using var source = parameter.CreateShapeSource(context);

        var rendering = RenderFrame(context, source, 0, Silent);

        Assert.Equal(new Bgra(204, 102, 51, 153).Premultiplied(), rendering[0, -1]);
    }

    [Theory]
    [InlineData(20d, -21)]
    [InlineData(40d, -41)]
    public void TheAmplitudeReachesTheString(double amplitude, int swungRow)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter(amplitude: amplitude).CreateShapeSource(context);

        var rendering = RenderFrame(context, source, 0, [1f]);

        Assert.Equal(White, rendering[0, swungRow]);
        Assert.Equal(White, rendering[0, swungRow + 1]);
        Assert.Equal(Bgra.Transparent, rendering[0, swungRow + 3]);
        Assert.Equal(Bgra.Transparent, rendering[0, -1]);
    }

    [Fact]
    public void ASingleBandAmongTwoIsScaledByTheSquareRootOfTheModeCount()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter().CreateShapeSource(context);

        var rendering = RenderFrame(context, source, 0, [1f, 0f]);

        Assert.Equal(White, rendering[0, -15]);
        Assert.Equal(Bgra.Transparent, rendering[0, -19]);
        Assert.Equal(Bgra.Transparent, rendering[0, -11]);
    }

    [Fact]
    public void LoudBandsAreCappedSoTheStringDoesNotGrowFurther()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter().CreateShapeSource(context);

        var loud = RenderFrame(context, source, 0, [1f, 1f, 1f, 1f]);
        var half = RenderFrame(context, source, 0, [0.5f, 0.5f, 0.5f, 0.5f]);
        var quiet = RenderFrame(context, source, 0, [0.25f, 0.25f, 0.25f, 0.25f]);

        Assert.True(loud.SamePixelsAs(half));
        Assert.False(half.SamePixelsAs(quiet));
    }

    [Fact]
    public void TheThicknessReachesTheString()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var thin = Parameter(thickness: 3d).CreateShapeSource(context);
        using var thick = Parameter(thickness: 7d).CreateShapeSource(context);

        Assert.Equal(2, OpaqueRows(RenderFrame(context, thin, 0, Silent), 0));
        Assert.Equal(6, OpaqueRows(RenderFrame(context, thick, 0, Silent), 0));
    }

    [Fact]
    public void TheStringLengthCapsTheModesAtAThirdOfItsPixels()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        float[] fifthBandOnly = [0f, 0f, 0f, 0f, 1f];
        float[] silent = new float[5];
        using var twelve = Parameter(12d).CreateShapeSource(context);
        using var fifteen = Parameter(15d).CreateShapeSource(context);

        var twelveSwinging = RenderFrame(context, twelve, 0, fifthBandOnly);
        var twelveSilent = RenderFrame(context, twelve, 0, silent);
        var fifteenSwinging = RenderFrame(context, fifteen, 0, fifthBandOnly);
        var fifteenSilent = RenderFrame(context, fifteen, 0, silent);

        Assert.True(twelveSwinging.SamePixelsAs(twelveSilent));
        Assert.False(fifteenSwinging.SamePixelsAs(fifteenSilent));
    }

    [Fact]
    public void AStringShorterThanThreePixelsStillCarriesOneMode()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter(2d).CreateShapeSource(context);

        var rendering = RenderFrame(context, source, 0, [1f]);

        Assert.Contains(rendering.Coordinates(), point => rendering[point.X, point.Y].Alpha > 0);
    }

    [Fact]
    public void TheModeLimitCapsTheSuperposedModes()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        float[] tenthBandOnly = [0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f];
        float[] silent = new float[10];
        using var five = Parameter(modeLimit: 5).CreateShapeSource(context);
        using var many = Parameter(modeLimit: 24).CreateShapeSource(context);

        var fiveSwinging = RenderFrame(context, five, 0, tenthBandOnly);
        var fiveSilent = RenderFrame(context, five, 0, silent);
        var manySwinging = RenderFrame(context, many, 0, tenthBandOnly);
        var manySilent = RenderFrame(context, many, 0, silent);

        Assert.True(fiveSwinging.SamePixelsAs(fiveSilent));
        Assert.False(manySwinging.SamePixelsAs(manySilent));
    }

    [Fact]
    public void TheBaseFrequencyMovesTheStringOverTime()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        float[] twoBands = [1f, 0.5f];
        using var still = Parameter(baseFrequency: 0d).CreateShapeSource(context);
        using var vibrating = Parameter(baseFrequency: 1.2d).CreateShapeSource(context);

        var stillFirst = RenderFrame(context, still, 0, twoBands);
        var stillLater = RenderFrame(context, still, 5, twoBands);
        var vibratingFirst = RenderFrame(context, vibrating, 0, twoBands);
        var vibratingLater = RenderFrame(context, vibrating, 5, twoBands);

        Assert.True(stillFirst.SamePixelsAs(stillLater));
        Assert.False(vibratingFirst.SamePixelsAs(vibratingLater));
    }

    [Fact]
    public void EachModeVibratesAtItsOwnMultipleOfTheBaseFrequency()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var first = Parameter(baseFrequency: 1d).CreateShapeSource(context);
        using var second = Parameter(baseFrequency: 1d).CreateShapeSource(context);

        var firstStart = RenderFrame(context, first, 0, [1f, 0f]);
        var firstHalf = RenderFrame(context, first, ItemDescriptions.Fps / 2, [1f, 0f]);
        var secondStart = RenderFrame(context, second, 0, [0f, 1f]);
        var secondQuarter = RenderFrame(context, second, 7, [0f, 1f]);
        var secondHalf = RenderFrame(context, second, ItemDescriptions.Fps / 2, [0f, 1f]);

        Assert.False(firstStart.SamePixelsAs(firstHalf));
        Assert.False(secondStart.SamePixelsAs(secondQuarter));
        Assert.True(secondStart.SamePixelsAs(secondHalf));
    }

    [Fact]
    public void ASixthOfThePeriodLaterTheFirstModeIsAtHalfItsSwing()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter(amplitude: 40d, baseFrequency: 1d).CreateShapeSource(context);

        var rendering = RenderFrame(context, source, ItemDescriptions.Fps / 6, [1f]);

        Assert.Equal(White, rendering[0, -21]);
        Assert.Equal(White, rendering[0, -20]);
        Assert.Equal(Bgra.Transparent, rendering[0, -41]);
        Assert.Equal(Bgra.Transparent, rendering[0, -1]);
    }

    [Fact]
    public void HalfAPeriodLaterTheFirstModeIsMirrored()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter(baseFrequency: 1d).CreateShapeSource(context);

        var start = RenderFrame(context, source, 0, [1f]);
        var half = RenderFrame(context, source, ItemDescriptions.Fps / 2, [1f]);

        Assert.Equal(White, start[0, -21]);
        Assert.Equal(White, half[0, 20]);
        Assert.All(start.Coordinates(), point => Assert.True(WithinRounding(start[point.X, point.Y], half[point.X, -1 - point.Y]), $"({point.X}, {point.Y})"));
    }

    [Fact]
    public void AnimatedAmplitudeIsReadAtEachFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var parameter = Parameter();
        parameter.Amplitude.CopyFrom(Linear(0d, 40d));
        using var source = parameter.CreateShapeSource(context);

        var start = RenderFrame(context, source, 0, [1f]);
        var end = RenderFrame(context, source, Length - 1, [1f]);

        Assert.Equal(White, start[0, -1]);
        Assert.Equal(White, start[0, 0]);
        Assert.Equal(Bgra.Transparent, end[0, -1]);
        Assert.Equal(Bgra.Transparent, end[0, 0]);
        Assert.Contains(end.Rows().Where(y => y < -30), y => end[0, y] == White);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    public void ANegativeBaseFrequencyVibratesLikeThePositiveOne(int frame)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        float[] twoBands = [1f, 0.5f];
        using var forward = Parameter(baseFrequency: 1d).CreateShapeSource(context);
        using var backward = Parameter(baseFrequency: -1d).CreateShapeSource(context);

        var start = RenderFrame(context, forward, 0, twoBands);
        var positive = RenderFrame(context, forward, frame, twoBands);
        var negative = RenderFrame(context, backward, frame, twoBands);

        Assert.False(start.SamePixelsAs(positive));
        Assert.Equal((positive.Left, positive.Top, positive.Width, positive.Height), (negative.Left, negative.Top, negative.Width, negative.Height));
        Assert.All(positive.Coordinates(), point => Assert.True(WithinRounding(positive[point.X, point.Y], negative[point.X, point.Y]), $"({point.X}, {point.Y})"));
    }

    [Fact]
    public void TheStringReturnsToItsShapeAfterOneBasePeriod()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        float[] twoBands = [1f, 0.5f];
        using var source = Parameter(baseFrequency: 1d).CreateShapeSource(context);

        var start = RenderFrame(context, source, 0, twoBands);
        var half = RenderFrame(context, source, ItemDescriptions.Fps / 2, twoBands);
        var period = RenderFrame(context, source, ItemDescriptions.Fps, twoBands);

        Assert.False(start.SamePixelsAs(half));
        Assert.True(start.SamePixelsAs(period));
    }

    public static readonly TheoryData<string, Action<StringSpectrumParameter>> LaterChanges = new()
    {
        { nameof(StringSpectrumParameter.StringWidth), parameter => parameter.StringWidth.Values[0].Value = 300d },
        { nameof(StringSpectrumParameter.Amplitude), parameter => parameter.Amplitude.Values[0].Value = 40d },
        { nameof(StringSpectrumParameter.BaseFrequency), parameter => parameter.BaseFrequency.Values[0].Value = 1.2d },
        { nameof(StringSpectrumParameter.ModeLimit), parameter => parameter.ModeLimit = 1 },
        { nameof(StringSpectrumParameter.Thickness), parameter => parameter.Thickness.Values[0].Value = 7d },
        { nameof(Color.R), parameter => parameter.StringColor = Color.FromArgb(255, 0, 255, 255) },
        { nameof(Color.G), parameter => parameter.StringColor = Color.FromArgb(255, 255, 0, 255) },
        { nameof(Color.B), parameter => parameter.StringColor = Color.FromArgb(255, 255, 255, 0) },
        { nameof(Color.A), parameter => parameter.StringColor = Color.FromArgb(128, 255, 255, 255) },
    };

    [Theory]
    [MemberData(nameof(LaterChanges))]
    public void EverySettingChangedAfterTheFirstFrameReachesTheString(string setting, Action<StringSpectrumParameter> change)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        float[] twoBands = [1f, 0.5f];
        var parameter = Parameter();
        var changed = Parameter();
        change(changed);
        using var source = parameter.CreateShapeSource(context);
        using var rebuilt = changed.CreateShapeSource(context);

        var before = RenderFrame(context, source, 5, twoBands);
        change(parameter);
        var after = RenderFrame(context, source, 5, twoBands);
        var expected = RenderFrame(context, rebuilt, 5, twoBands);

        Assert.False(before.SamePixelsAs(after), setting);
        Assert.True(after.SamePixelsAs(expected), setting);
    }

    [Fact]
    public void RaisingTheModeLimitPastABlockOfFourReachesTheString()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        float[] fifthBandOnly = [0f, 0f, 0f, 0f, 1f];
        var parameter = Parameter(modeLimit: 4);
        using var source = parameter.CreateShapeSource(context);
        using var rebuilt = Parameter(modeLimit: 5).CreateShapeSource(context);

        var before = RenderFrame(context, source, 0, fifthBandOnly);
        parameter.ModeLimit = 5;
        var after = RenderFrame(context, source, 0, fifthBandOnly);
        var expected = RenderFrame(context, rebuilt, 0, fifthBandOnly);

        Assert.False(before.SamePixelsAs(after));
        Assert.True(after.SamePixelsAs(expected));
    }

    [Fact]
    public void AChangedSpectrumReachesTheNextFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter().CreateShapeSource(context);

        var up = RenderFrame(context, source, 0, [1f]);
        var down = RenderFrame(context, source, 0, [-1f]);

        Assert.Equal(White, up[0, -21]);
        Assert.Equal(White, down[0, 20]);
        Assert.False(up.SamePixelsAs(down));
    }

    [Fact]
    public void TheSameInputsAlwaysProduceTheSamePixels()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var first = Parameter().CreateShapeSource(context);
        using var second = Parameter().CreateShapeSource(context);

        var one = RenderFrame(context, first, 3, Loud);
        var again = RenderFrame(context, first, 3, Loud);
        var other = RenderFrame(context, second, 3, Loud);

        Assert.True(one.SamePixelsAs(again));
        Assert.True(one.SamePixelsAs(other));
    }

    [Fact]
    public void AFailureWhileUpdatingIsNotSwallowed()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = Parameter().CreateShapeSource(context);

        Assert.ThrowsAny<Exception>(() => source.Update(null!, Silent));
    }
}

using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using static StringSpectrum.StringSpectrumCustomEffect;

namespace StringSpectrum.Tests;

[Collection("Direct2D")]
public sealed class StringSpectrumCustomEffectTests
{
    const string ModesName = "Modes";

    static float Read(StringSpectrumCustomEffect effect, PropertyIndex index) => effect.GetFloatValue((int)index);

    static byte[] ReadModes(StringSpectrumCustomEffect effect)
    {
        var modes = new byte[effect.GetValueSize((int)PropertyIndex.Modes)];
        effect.GetValueByName(ModesName, PropertyType.Blob, modes, modes.Length);
        return modes;
    }

    static void Write(StringSpectrumCustomEffect effect, int channel, float value)
    {
        switch ((PropertyIndex)channel)
        {
            case PropertyIndex.ColorR:
                effect.ColorR = value;
                break;
            case PropertyIndex.ColorG:
                effect.ColorG = value;
                break;
            case PropertyIndex.ColorB:
                effect.ColorB = value;
                break;
            default:
                effect.ColorA = value;
                break;
        }
    }

    [Fact]
    public void TheEffectIsEnabledOnceCreated()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new StringSpectrumCustomEffect(context);

        Assert.True(effect.IsEnabled);
    }

    [Fact]
    public void ThePropertiesStartFromZero()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new StringSpectrumCustomEffect(context);

        Assert.Equal(0f, Read(effect, PropertyIndex.Width));
        Assert.Equal(0f, Read(effect, PropertyIndex.Amplitude));
        Assert.Equal(0f, Read(effect, PropertyIndex.ModeCount));
        Assert.Equal(0f, Read(effect, PropertyIndex.Thickness));
        Assert.Equal(0f, Read(effect, PropertyIndex.ColorR));
        Assert.Equal(0f, Read(effect, PropertyIndex.ColorG));
        Assert.Equal(0f, Read(effect, PropertyIndex.ColorB));
        Assert.Equal(0f, Read(effect, PropertyIndex.ColorA));
        Assert.Equal(new byte[ModeByteSize], ReadModes(effect));
    }

    [Theory]
    [InlineData(-1f, 0.001f)]
    [InlineData(0f, 0.001f)]
    [InlineData(0.0005f, 0.001f)]
    [InlineData(0.001f, 0.001f)]
    [InlineData(600f, 600f)]
    [InlineData(float.MaxValue, float.MaxValue)]
    public void TheWidthNeverDropsBelowAThousandthOfAPixel(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new StringSpectrumCustomEffect(context);
        effect.Width = 5f;

        effect.Width = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.Width));
    }

    [Theory]
    [InlineData(-5f)]
    [InlineData(0f)]
    [InlineData(120f)]
    [InlineData(float.MaxValue)]
    public void TheAmplitudeIsStoredWithoutClamping(float value)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new StringSpectrumCustomEffect(context);

        effect.Amplitude = value;

        Assert.Equal(value, Read(effect, PropertyIndex.Amplitude));
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(2.5f, 2.5f)]
    [InlineData(24f, 24f)]
    [InlineData((float)MaxModes, (float)MaxModes)]
    [InlineData(MaxModes + 1f, (float)MaxModes)]
    [InlineData(float.MaxValue, (float)MaxModes)]
    public void TheModeCountIsClampedToTheShaderCapacity(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new StringSpectrumCustomEffect(context);

        effect.ModeCount = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.ModeCount));
    }

    [Theory]
    [InlineData(-1f, 0.01f)]
    [InlineData(0f, 0.01f)]
    [InlineData(0.005f, 0.01f)]
    [InlineData(0.01f, 0.01f)]
    [InlineData(3f, 3f)]
    [InlineData(float.MaxValue, float.MaxValue)]
    public void TheThicknessNeverDropsBelowAHundredthOfAPixel(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new StringSpectrumCustomEffect(context);
        effect.Thickness = 5f;

        effect.Thickness = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.Thickness));
    }

    [Theory]
    [InlineData((int)PropertyIndex.ColorR, -0.5f, 0f)]
    [InlineData((int)PropertyIndex.ColorR, 0.25f, 0.25f)]
    [InlineData((int)PropertyIndex.ColorR, 1.5f, 1f)]
    [InlineData((int)PropertyIndex.ColorG, -0.5f, 0f)]
    [InlineData((int)PropertyIndex.ColorG, 0.25f, 0.25f)]
    [InlineData((int)PropertyIndex.ColorG, 1.5f, 1f)]
    [InlineData((int)PropertyIndex.ColorB, -0.5f, 0f)]
    [InlineData((int)PropertyIndex.ColorB, 0.25f, 0.25f)]
    [InlineData((int)PropertyIndex.ColorB, 1.5f, 1f)]
    [InlineData((int)PropertyIndex.ColorA, -0.5f, 0f)]
    [InlineData((int)PropertyIndex.ColorA, 0.25f, 0.25f)]
    [InlineData((int)PropertyIndex.ColorA, 1.5f, 1f)]
    public void EveryColorChannelIsClampedToTheUnitRange(int channel, float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new StringSpectrumCustomEffect(context);

        Write(effect, channel, value);

        Assert.Equal(expected, effect.GetFloatValue(channel));
    }

    [Fact]
    public void AShorterModeBlockFillsTheFrontAndClearsTheRest()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new StringSpectrumCustomEffect(context);
        effect.Modes = Enumerable.Repeat((byte)7, ModeByteSize).ToArray();

        effect.Modes = [1, 2, 3];

        var expected = new byte[ModeByteSize];
        expected[0] = 1;
        expected[1] = 2;
        expected[2] = 3;
        Assert.Equal(expected, ReadModes(effect));
    }

    [Fact]
    public void ALongerModeBlockIsTruncatedToTheShaderCapacity()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new StringSpectrumCustomEffect(context);

        effect.Modes = Enumerable.Range(1, ModeByteSize + 40).Select(index => (byte)index).ToArray();

        Assert.Equal(Enumerable.Range(1, ModeByteSize).Select(index => (byte)index), ReadModes(effect));
    }
}

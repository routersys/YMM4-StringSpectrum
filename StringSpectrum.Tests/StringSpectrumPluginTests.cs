using YukkuriMovieMaker.Plugin.Shape;

namespace StringSpectrum.Tests;

public sealed class StringSpectrumPluginTests
{
    [Fact]
    public void TheNameIsTheLocalizedSpectrumName()
    {
        var plugin = new StringSpectrumPlugin();

        Assert.Equal(Texts.StringSpectrum, plugin.Name);
    }

    [Fact]
    public void AviUtlOutputIsNotSupported()
    {
        var plugin = new StringSpectrumPlugin();

        Assert.False(plugin.IsExoShapeSupported);
        Assert.False(plugin.IsExoMaskSupported);
    }

    [Fact]
    public void ThePluginIsDiscoveredAsAnAudioSpectrum()
    {
        Assert.IsAssignableFrom<IAudioSpectrumPlugin>(new StringSpectrumPlugin());
    }

    [Fact]
    public void WithoutSharedDataTheParameterStartsFromItsDefaults()
    {
        var plugin = new StringSpectrumPlugin();

        var parameter = Assert.IsType<StringSpectrumParameter>(plugin.CreateAudioSpectrumParameter(null));

        Assert.Equal(24, parameter.ModeLimit);
    }

    [Fact]
    public void TheSharedDataOfThePreviousParameterIsCarriedOver()
    {
        var plugin = new StringSpectrumPlugin();
        var previous = new StringSpectrumParameter { ModeLimit = 3 };
        previous.Amplitude.Values[0].Value = 7d;

        var parameter = Assert.IsType<StringSpectrumParameter>(plugin.CreateAudioSpectrumParameter(previous.GetSharedData()));

        Assert.NotSame(previous, parameter);
        Assert.Equal(3, parameter.ModeLimit);
        Assert.Equal(7d, parameter.Amplitude.GetValue(0, 1, ItemDescriptions.Fps));
    }

    [Fact]
    public void EveryCallCreatesANewParameter()
    {
        var plugin = new StringSpectrumPlugin();

        Assert.NotSame(plugin.CreateAudioSpectrumParameter(null), plugin.CreateAudioSpectrumParameter(null));
    }
}

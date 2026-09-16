using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace StringSpectrum.Tests;

public sealed class StringSpectrumParameterTests
{
    const double VeryLargeValue = YMM4Constants.VeryLargeValue;

    static PropertyInfo Property(string name) => typeof(StringSpectrumParameter).GetProperty(name)!;

    static T Attribute<T>(string property) where T : Attribute => Property(property).GetCustomAttribute<T>()!;

    static ExoOutputDescription ExoDescription() => new(new VideoInfo(), string.Empty, new AviUtlDirectories(string.Empty, string.Empty));

    static AudioSpectrumExoOutputDescription SpectrumDescription() => new(string.Empty, new Animation(100, 0, 100), TimeSpan.Zero, new Animation(32, 1, 1000));

    static ShapeMaskExoOutputDescription MaskDescription() => new(true, new Animation(0, -VeryLargeValue, VeryLargeValue), new Animation(0, -VeryLargeValue, VeryLargeValue), new Animation(0, -VeryLargeValue, VeryLargeValue), new Animation(0, 0, VeryLargeValue), false);

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}"""))!;

    static StringSpectrumParameter Configured()
    {
        var parameter = new StringSpectrumParameter { ModeLimit = 8, StringColor = Color.FromArgb(200, 10, 20, 30) };
        parameter.StringWidth.Values[0].Value = 300d;
        parameter.Amplitude.Values[0].Value = 45d;
        parameter.BaseFrequency.Values[0].Value = -2.5d;
        parameter.Thickness.Values[0].Value = 6d;
        return parameter;
    }

    static void AssertConfigured(StringSpectrumParameter parameter)
    {
        Assert.Equal(8, parameter.ModeLimit);
        Assert.Equal(Color.FromArgb(200, 10, 20, 30), parameter.StringColor);
        Assert.Equal(300d, parameter.StringWidth.GetValue(0, 1, ItemDescriptions.Fps));
        Assert.Equal(45d, parameter.Amplitude.GetValue(0, 1, ItemDescriptions.Fps));
        Assert.Equal(-2.5d, parameter.BaseFrequency.GetValue(0, 1, ItemDescriptions.Fps));
        Assert.Equal(6d, parameter.Thickness.GetValue(0, 1, ItemDescriptions.Fps));
    }

    [Theory]
    [InlineData(nameof(StringSpectrumParameter.StringWidth), 600d, 0.01d, VeryLargeValue)]
    [InlineData(nameof(StringSpectrumParameter.Amplitude), 120d, 0d, VeryLargeValue)]
    [InlineData(nameof(StringSpectrumParameter.BaseFrequency), 1.2d, -VeryLargeValue, VeryLargeValue)]
    [InlineData(nameof(StringSpectrumParameter.Thickness), 3d, 0.01d, VeryLargeValue)]
    public void AnimatedParametersStartFromTheirDefaultsWithinTheirRange(string name, double defaultValue, double minimum, double maximum)
    {
        var parameter = new StringSpectrumParameter();

        var animation = (Animation)Property(name).GetValue(parameter)!;

        Assert.Equal(defaultValue, animation.DefaultValue);
        Assert.Equal(minimum, animation.MinValue);
        Assert.Equal(maximum, animation.MaxValue);
        Assert.Equal(defaultValue, animation.GetValue(0, 1, ItemDescriptions.Fps));
    }

    [Fact]
    public void TheModeLimitAndTheColorStartFromTheirDefaults()
    {
        var parameter = new StringSpectrumParameter();

        Assert.Equal(24, parameter.ModeLimit);
        Assert.Equal(Colors.White, parameter.StringColor);
    }

    [Theory]
    [InlineData(int.MinValue, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(24, 24)]
    [InlineData(StringSpectrumCustomEffect.MaxModes, StringSpectrumCustomEffect.MaxModes)]
    [InlineData(StringSpectrumCustomEffect.MaxModes + 1, StringSpectrumCustomEffect.MaxModes)]
    [InlineData(int.MaxValue, StringSpectrumCustomEffect.MaxModes)]
    public void TheModeLimitStaysBetweenOneAndTheShaderCapacity(int value, int expected)
    {
        var parameter = new StringSpectrumParameter { ModeLimit = 5 };

        parameter.ModeLimit = value;

        Assert.Equal(expected, parameter.ModeLimit);
        Assert.False(parameter.HasErrors);
    }

    [Fact]
    public void ChangingTheModeLimitOrTheColorNotifiesTheEditor()
    {
        var parameter = new StringSpectrumParameter();
        var changed = new List<string?>();
        parameter.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        parameter.ModeLimit = 2;
        parameter.StringColor = Colors.Red;

        Assert.Equal([nameof(StringSpectrumParameter.ModeLimit), nameof(StringSpectrumParameter.StringColor)], changed);
    }

    [Fact]
    public void AssigningAnUnchangedOrClampedValueDoesNotNotify()
    {
        var parameter = new StringSpectrumParameter { ModeLimit = 1 };
        var changed = new List<string?>();
        parameter.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        parameter.ModeLimit = 1;
        parameter.ModeLimit = -5;
        parameter.StringColor = Colors.White;

        Assert.Empty(changed);
    }

    [Fact]
    public void TheFourAnimatedParametersReceiveTheAnimationParameters()
    {
        var parameter = new StringSpectrumParameter();

        parameter.SetAnimationParameters(120, ItemDescriptions.Fps);

        Assert.All([parameter.StringWidth, parameter.Amplitude, parameter.BaseFrequency, parameter.Thickness], animation => Assert.Equal(120, animation.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void NoExoFilterIsWrittenForAviUtl(int keyFrameIndex)
    {
        var parameter = new StringSpectrumParameter();

        Assert.Empty(parameter.CreateShapeItemExoFilter(keyFrameIndex, ExoDescription(), SpectrumDescription()));
        Assert.Empty(parameter.CreateMaskExoFilter(keyFrameIndex, ExoDescription(), MaskDescription(), SpectrumDescription()));
    }

    [Theory]
    [InlineData(nameof(StringSpectrumParameter.StringWidth), nameof(Texts.StringWidth), nameof(Texts.StringWidthDescription), 10)]
    [InlineData(nameof(StringSpectrumParameter.Amplitude), nameof(Texts.Amplitude), nameof(Texts.AmplitudeDescription), 11)]
    [InlineData(nameof(StringSpectrumParameter.BaseFrequency), nameof(Texts.BaseFrequency), nameof(Texts.BaseFrequencyDescription), 12)]
    [InlineData(nameof(StringSpectrumParameter.ModeLimit), nameof(Texts.ModeLimit), nameof(Texts.ModeLimitDescription), 13)]
    [InlineData(nameof(StringSpectrumParameter.Thickness), nameof(Texts.Thickness), nameof(Texts.ThicknessDescription), 14)]
    [InlineData(nameof(StringSpectrumParameter.StringColor), nameof(Texts.StringColor), nameof(Texts.StringColorDescription), 15)]
    public void EveryParameterIsDisplayedInOrder(string property, string name, string description, int order)
    {
        var display = Attribute<DisplayAttribute>(property);

        Assert.Null(display.GroupName);
        Assert.Equal(name, display.Name);
        Assert.Equal(description, display.Description);
        Assert.Equal(order, display.Order);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Theory]
    [InlineData(nameof(StringSpectrumParameter.StringWidth), "F1", "px", 0d, 1920d)]
    [InlineData(nameof(StringSpectrumParameter.Amplitude), "F1", "px", 0d, 400d)]
    [InlineData(nameof(StringSpectrumParameter.BaseFrequency), "F2", "", 0d, 10d)]
    [InlineData(nameof(StringSpectrumParameter.Thickness), "F1", "px", 0d, 30d)]
    public void AnimatedParametersAreEditedWithAnimationSliders(string property, string format, string unit, double minimum, double maximum)
    {
        var slider = Attribute<AnimationSliderAttribute>(property);

        Assert.Equal(format, slider.StringFormat);
        Assert.Equal(unit, slider.UnitText);
        Assert.Equal(minimum, slider.DefaultMin);
        Assert.Equal(maximum, slider.DefaultMax);
    }

    [Fact]
    public void TheModeLimitIsEditedWithoutAUnitUpToTheShaderCapacity()
    {
        var slider = Attribute<TextBoxSliderAttribute>(nameof(StringSpectrumParameter.ModeLimit));

        Assert.Equal("F0", slider.StringFormat);
        Assert.Equal(string.Empty, slider.UnitText);
        Assert.Equal(1d, slider.DefaultMin);
        Assert.Equal(StringSpectrumCustomEffect.MaxModes, slider.DefaultMax);
    }

    [Fact]
    public void TheColorIsEditedWithAColorPicker()
    {
        Assert.NotNull(Attribute<ColorPickerAttribute>(nameof(StringSpectrumParameter.StringColor)));
    }

    [Fact]
    public void EverySettingSurvivesAProjectRoundTrip()
    {
        var parameter = Configured();

        var clone = Json.GetClone(parameter)!;

        Assert.NotSame(parameter, clone);
        AssertConfigured(clone);
    }

    [Fact]
    public void KeyframedAnimationsSurviveAProjectRoundTrip()
    {
        const int length = 60;
        var parameter = new StringSpectrumParameter();
        parameter.StringWidth.CopyFrom(Linear(100d, 300d));

        var clone = Json.GetClone(parameter)!;

        Assert.NotEqual(parameter.StringWidth.GetValue(0, length, ItemDescriptions.Fps), parameter.StringWidth.GetValue(length - 1, length, ItemDescriptions.Fps));
        Assert.All([0, length / 2, length - 1], frame => Assert.Equal(parameter.StringWidth.GetValue(frame, length, ItemDescriptions.Fps), clone.StringWidth.GetValue(frame, length, ItemDescriptions.Fps)));
    }

    [Fact]
    public void EverySettingIsCarriedThroughTheSharedData()
    {
        var parameter = Configured();

        var restored = new StringSpectrumParameter(parameter.GetSharedData());

        Assert.NotSame(parameter, restored);
        AssertConfigured(restored);
    }

    [Fact]
    public void AnEmptySharedDataLeavesTheDefaults()
    {
        var parameter = new StringSpectrumParameter(new SharedDataStore());

        Assert.Equal(24, parameter.ModeLimit);
        Assert.Equal(Colors.White, parameter.StringColor);
        Assert.Equal(600d, parameter.StringWidth.GetValue(0, 1, ItemDescriptions.Fps));
    }

    [Fact]
    public void TheSharedDataOfAnotherKindIsIgnored()
    {
        var store = new SharedDataStore();
        store.Save(new object());

        var parameter = new StringSpectrumParameter(store);

        Assert.Equal(24, parameter.ModeLimit);
    }

    [Fact]
    public void AFailureWhileCreatingTheSourceIsNotSwallowed()
    {
        var parameter = new StringSpectrumParameter();

        Assert.ThrowsAny<Exception>(() => parameter.CreateShapeSource(null!));
    }
}

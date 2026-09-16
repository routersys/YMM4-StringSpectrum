using System.Windows.Media;

namespace StringSpectrum.Harness;

internal static class HarnessCases
{
    public static IEnumerable<(string Name, StringSpectrumParameter Parameter, IReadOnlyList<int> Frames)> All()
    {
        yield return ("default", Create(), [0]);
        yield return ("default-frame-4", Create(), [4]);
        yield return ("default-frames-0-8", Create(), Enumerable.Range(0, 9).ToArray());
        yield return ("width-200", Create(parameter => parameter.StringWidth.Values[0].Value = 200), [0]);
        yield return ("width-1200", Create(parameter => parameter.StringWidth.Values[0].Value = 1200), [0]);
        yield return ("amplitude-0", Create(parameter => parameter.Amplitude.Values[0].Value = 0), [0]);
        yield return ("amplitude-400", Create(parameter => parameter.Amplitude.Values[0].Value = 400), [0]);
        yield return ("frequency-0-frame-4", Create(parameter => parameter.BaseFrequency.Values[0].Value = 0), [4]);
        yield return ("frequency-10-frame-4", Create(parameter => parameter.BaseFrequency.Values[0].Value = 10), [4]);
        yield return ("modes-1", Create(parameter => parameter.ModeLimit = 1), [0]);
        yield return ("modes-64", Create(parameter => parameter.ModeLimit = 64), [0]);
        yield return ("thickness-1", Create(parameter => parameter.Thickness.Values[0].Value = 1), [0]);
        yield return ("thickness-30", Create(parameter => parameter.Thickness.Values[0].Value = 30), [0]);
        yield return ("color-half-red", Create(parameter => parameter.StringColor = Color.FromArgb(128, 255, 0, 0)), [0]);
    }

    public static IEnumerable<(string Name, Func<StringSpectrumParameter> Create, Action<StringSpectrumParameter> Change, int Frame)> Transitions()
    {
        yield return ("width-600-to-200", () => Create(), parameter => parameter.StringWidth.Values[0].Value = 200, 0);
        yield return ("amplitude-120-to-0", () => Create(), parameter => parameter.Amplitude.Values[0].Value = 0, 0);
        yield return ("amplitude-0-to-120", () => Create(parameter => parameter.Amplitude.Values[0].Value = 0), parameter => parameter.Amplitude.Values[0].Value = 120, 0);
        yield return ("frequency-1.2-to-0-frame-4", () => Create(), parameter => parameter.BaseFrequency.Values[0].Value = 0, 4);
        yield return ("modes-24-to-1", () => Create(), parameter => parameter.ModeLimit = 1, 0);
        yield return ("modes-24-to-64", () => Create(), parameter => parameter.ModeLimit = 64, 0);
        yield return ("thickness-3-to-30", () => Create(), parameter => parameter.Thickness.Values[0].Value = 30, 0);
        yield return ("color-white-to-half-red", () => Create(), parameter => parameter.StringColor = Color.FromArgb(128, 255, 0, 0), 0);
    }

    public static IEnumerable<(string Name, StringSpectrumParameter Parameter)> Benchmarks()
    {
        yield return ("default", Create());
        yield return ("modes-64", Create(parameter => parameter.ModeLimit = 64));
        yield return ("width-1200", Create(parameter => parameter.StringWidth.Values[0].Value = 1200));
        yield return ("thickness-30", Create(parameter => parameter.Thickness.Values[0].Value = 30));
    }

    static StringSpectrumParameter Create(Action<StringSpectrumParameter>? configure = null)
    {
        var parameter = new StringSpectrumParameter();
        configure?.Invoke(parameter);
        return parameter;
    }
}

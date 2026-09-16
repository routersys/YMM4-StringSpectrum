namespace StringSpectrum.Harness;

internal static class HarnessCases
{
    public static IEnumerable<(string Name, StringSpectrumParameter Parameter, IReadOnlyList<int> Frames)> All()
    {
        yield break;
    }

    public static IEnumerable<(string Name, Func<StringSpectrumParameter> Create, Action<StringSpectrumParameter> Change, int Frame)> Transitions()
    {
        yield break;
    }

    public static IEnumerable<(string Name, StringSpectrumParameter Parameter)> Benchmarks()
    {
        yield break;
    }
}

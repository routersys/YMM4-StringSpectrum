using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace StringSpectrum.Harness;

internal static class SyntheticSpectrum
{
    public const int Bands = 32;
    const double BandStep = 0.11;
    const double FrameStep = 0.07;
    const int KeyLength = 16;

    public static float[] At(int frame)
    {
        var bands = new float[Bands];
        for (var band = 0; band < Bands; band++)
        {
            var phase = 2.0 * Math.PI * (band * BandStep + frame * FrameStep);
            bands[band] = (float)((0.5 + 0.5 * Math.Sin(phase)) * (1.0 - band / (double)Bands));
        }

        return bands;
    }

    public static string Identity(int frames)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (var frame = 0; frame < frames; frame++)
            hash.AppendData(MemoryMarshal.AsBytes(At(frame).AsSpan()));
        return $"synthetic {Bands} bands {frames} frames {Convert.ToHexString(hash.GetHashAndReset())[..KeyLength]}";
    }
}

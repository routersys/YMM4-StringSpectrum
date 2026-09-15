namespace StringSpectrum;

internal static class ShaderResourceUri
{
    public static Uri Get(string shaderName) => new($"pack://application:,,,/StringSpectrum;component/Resources/Shader/{shaderName}.cso", UriKind.Absolute);
}

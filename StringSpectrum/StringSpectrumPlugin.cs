using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace StringSpectrum
{
    [PluginDetails(AuthorName = "routersys")]
    internal class StringSpectrumPlugin : IAudioSpectrumPlugin
    {
        public string Name => Texts.StringSpectrum;

        public bool IsExoShapeSupported => false;

        public bool IsExoMaskSupported => false;

        public IAudioSpectrumParameter CreateAudioSpectrumParameter(SharedDataStore? sharedData)
        {
            try
            {
                return new StringSpectrumParameter(sharedData);
            }
            catch (Exception exception)
            {
                StringSpectrumTelemetry.Report(exception);
                throw;
            }
        }
    }
}

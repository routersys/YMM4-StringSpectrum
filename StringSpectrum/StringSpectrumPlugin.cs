using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace StringSpectrum
{
    internal class StringSpectrumPlugin : IAudioSpectrumPlugin
    {
        public string Name => Texts.StringSpectrum;

        public bool IsExoShapeSupported => false;

        public bool IsExoMaskSupported => false;

        public IAudioSpectrumParameter CreateAudioSpectrumParameter(SharedDataStore? sharedData)
        {
            return new StringSpectrumParameter(sharedData);
        }
    }
}

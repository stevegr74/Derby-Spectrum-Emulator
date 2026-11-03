using System;

namespace Derby.Audio
{
    public interface IAudioOutput : IDisposable
    {
        uint SampleRate { get; }
        void WriteSamples(short[] monoSamples);
    }
}

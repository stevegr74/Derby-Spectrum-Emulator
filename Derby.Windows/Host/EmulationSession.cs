using Derby.Audio;

namespace Derby
{
    internal sealed class EmulationSession : IDisposable
    {
        private bool disposed;

        internal EmulationSession(SpectrumMachine machine, AudioPipeline audioPipeline)
        {
            Machine = machine ?? throw new ArgumentNullException(nameof(machine));
            AudioPipeline = audioPipeline ?? throw new ArgumentNullException(nameof(audioPipeline));
        }

        internal object MachineSyncRoot { get; } = new();
        internal EmulationLoopController Loop { get; } = new();
        internal SpectrumMachine Machine { get; set; }
        internal AudioPipeline AudioPipeline { get; set; }

        public void Dispose()
        {
            if (disposed)
                return;

            Loop.Dispose();
            AudioPipeline.Dispose();
            disposed = true;
        }
    }
}

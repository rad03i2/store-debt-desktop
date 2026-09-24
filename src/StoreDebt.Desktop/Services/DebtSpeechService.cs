using System.Speech.Synthesis;

namespace StoreDebt.Desktop.Services;

public sealed class DebtSpeechService : IDisposable
{
    private readonly object _sync = new();
    private SpeechSynthesizer? _synthesizer;
    private bool _initializationAttempted;
    private bool _disposed;

    public async Task SpeakDebtAsync(long amount)
    {
        await Task.Delay(500);

        try
        {
            var synthesizer = EnsureSynthesizer();
            if (synthesizer is null) return;

            lock (_sync)
            {
                if (_disposed) return;
                synthesizer.SpeakAsyncCancelAll();
                synthesizer.SpeakAsync($"تم تسجيل دين بمبلغ {amount} دينار عراقي");
            }
        }
        catch
        {
            // Speech must never prevent the store app from working.
        }
    }

    private SpeechSynthesizer? EnsureSynthesizer()
    {
        lock (_sync)
        {
            if (_disposed) return null;
            if (_synthesizer is not null) return _synthesizer;
            if (_initializationAttempted) return null;

            _initializationAttempted = true;

            try
            {
                var synthesizer = new SpeechSynthesizer();

                var arabicVoice = synthesizer.GetInstalledVoices()
                    .FirstOrDefault(v =>
                        v.Enabled &&
                        v.VoiceInfo.Culture.Name.StartsWith(
                            "ar",
                            StringComparison.OrdinalIgnoreCase));

                if (arabicVoice is not null)
                    synthesizer.SelectVoice(arabicVoice.VoiceInfo.Name);

                synthesizer.Rate = 0;
                synthesizer.Volume = 100;
                _synthesizer = synthesizer;

                return _synthesizer;
            }
            catch
            {
                return null;
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _synthesizer?.SpeakAsyncCancelAll();
                _synthesizer?.Dispose();
            }
            catch
            {
            }

            _synthesizer = null;
        }
    }
}

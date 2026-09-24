using System.Speech.Synthesis;

namespace StoreDebt.Desktop.Services;

public sealed class DebtSpeechService : IDisposable
{
    private readonly object _sync = new();
    private SpeechSynthesizer? _synthesizer;
    private bool _initializationAttempted;
    private bool _disposed;

    public void Prepare()
    {
        try
        {
            _ = EnsureSynthesizer();
        }
        catch
        {
            // Speech is optional and must never block the app.
        }
    }

    public Task SpeakDebtAsync(long amount)
    {
        try
        {
            var synthesizer = EnsureSynthesizer();
            if (synthesizer is null)
                return Task.CompletedTask;

            lock (_sync)
            {
                if (_disposed)
                    return Task.CompletedTask;

                synthesizer.SpeakAsyncCancelAll();
                synthesizer.SpeakAsync($"تم تسجيل دين بمبلغ {amount} دينار عراقي");
            }
        }
        catch
        {
            // The debt is already saved; speech failure must not affect it.
        }

        return Task.CompletedTask;
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

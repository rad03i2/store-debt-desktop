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
            var synthesizer = EnsureSynthesizer();
            if (synthesizer is null) return;

            lock (_sync)
            {
                if (_disposed) return;

                // Prime the selected voice silently once so the first real debt
                // announcement does not pay the engine-startup cost.
                synthesizer.SetOutputToNull();
                synthesizer.Speak(" ");
                synthesizer.SetOutputToDefaultAudioDevice();
            }
        }
        catch
        {
            try
            {
                _synthesizer?.SetOutputToDefaultAudioDevice();
            }
            catch
            {
            }

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
                    .Where(v =>
                        v.Enabled &&
                        v.VoiceInfo.Culture.Name.StartsWith(
                            "ar",
                            StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(v => ScoreVoice(v.VoiceInfo))
                    .FirstOrDefault();

                if (arabicVoice is not null)
                    synthesizer.SelectVoice(arabicVoice.VoiceInfo.Name);

                // Keep speech cadence natural. Startup latency is handled by Prepare().
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

    private static int ScoreVoice(VoiceInfo voice)
    {
        var score = 0;
        var name = voice.Name ?? string.Empty;
        var culture = voice.Culture?.Name ?? string.Empty;

        if (culture.Equals("ar-IQ", StringComparison.OrdinalIgnoreCase))
            score += 120;
        else if (culture.StartsWith("ar", StringComparison.OrdinalIgnoreCase))
            score += 60;

        if (voice.Gender == VoiceGender.Female)
            score += 35;

        if (voice.Age == VoiceAge.Adult)
            score += 10;

        if (name.Contains("Natural", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Neural", StringComparison.OrdinalIgnoreCase))
            score += 80;

        return score;
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

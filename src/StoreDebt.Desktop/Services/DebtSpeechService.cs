using System.Globalization;
using System.Speech.Synthesis;

namespace StoreDebt.Desktop.Services;

public sealed class DebtSpeechService : IDisposable
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly object _sync = new();
    private bool _disposed;

    public DebtSpeechService()
    {
        try
        {
            var arabicVoice = _synthesizer.GetInstalledVoices()
                .FirstOrDefault(v =>
                    v.Enabled &&
                    v.VoiceInfo.Culture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase));

            if (arabicVoice is not null)
            {
                _synthesizer.SelectVoice(arabicVoice.VoiceInfo.Name);
            }

            _synthesizer.Rate = 0;
            _synthesizer.Volume = 100;
        }
        catch
        {
            // Speech is an enhancement; failure must never block store operations.
        }
    }

    public async Task SpeakDebtAsync(long amount)
    {
        await Task.Delay(500);

        if (_disposed) return;

        try
        {
            lock (_sync)
            {
                if (_disposed) return;
                _synthesizer.SpeakAsyncCancelAll();
                _synthesizer.SpeakAsync($"تم تسجيل دين بمبلغ {amount} دينار عراقي");
            }
        }
        catch
        {
            // Debt has already been safely saved; TTS failure is non-critical.
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.Dispose();
        }
    }
}

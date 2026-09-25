using System.IO;
using System.Speech.Synthesis;
using System.Text.Json;

namespace StoreDebt.Desktop.Services;

public sealed record SpeechVoiceOption(
    string Name,
    string DisplayName,
    bool IsSelected);

public sealed class DebtSpeechService : IDisposable
{
    private sealed class SpeechSettings
    {
        public string VoiceName { get; set; } = string.Empty;
        public string Speed { get; set; } = "طبيعي";
        public int Volume { get; set; } = 100;
    }

    private readonly object _sync = new();
    private SpeechSynthesizer? _synthesizer;
    private bool _initializationAttempted;
    private bool _disposed;
    private SpeechSettings _settings = LoadSettings();

    private static string SettingsPath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StoreDebtDesktop");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "speech-settings.json");
        }
    }

    public string SelectedVoiceName => _settings.VoiceName;
    public string Speed => _settings.Speed;
    public int Volume => _settings.Volume;

    public void Prepare()
    {
        try
        {
            var synthesizer = EnsureSynthesizer();
            if (synthesizer is null) return;

            lock (_sync)
            {
                if (_disposed) return;
                synthesizer.SetOutputToNull();
                synthesizer.Speak(" ");
                synthesizer.SetOutputToDefaultAudioDevice();
            }
        }
        catch
        {
            try { _synthesizer?.SetOutputToDefaultAudioDevice(); } catch { }
        }
    }

    public IReadOnlyList<SpeechVoiceOption> GetTopArabicVoices()
    {
        try
        {
            var synthesizer = EnsureSynthesizer();
            if (synthesizer is null)
                return [];

            lock (_sync)
            {
                var voices = synthesizer.GetInstalledVoices()
                    .Where(v =>
                        v.Enabled &&
                        v.VoiceInfo.Culture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(v => string.Equals(v.VoiceInfo.Name, _settings.VoiceName, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(v => ScoreVoice(v.VoiceInfo))
                    .Take(4)
                    .Select(v => new SpeechVoiceOption(
                        v.VoiceInfo.Name,
                        BuildDisplayName(v.VoiceInfo),
                        string.Equals(v.VoiceInfo.Name, _settings.VoiceName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                return voices;
            }
        }
        catch
        {
            return [];
        }
    }

    public void SaveConfiguration(string? voiceName, string speed, int volume)
    {
        lock (_sync)
        {
            if (_disposed) return;

            _settings.VoiceName = voiceName?.Trim() ?? string.Empty;
            _settings.Speed = NormalizeSpeed(speed);
            _settings.Volume = Math.Clamp(volume, 0, 100);

            var synthesizer = EnsureSynthesizer();
            ApplySettings(synthesizer, _settings);

            try
            {
                var json = JsonSerializer.Serialize(
                    _settings,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Saving settings must never break debt entry.
            }
        }
    }

    public Task TestVoiceAsync(string? voiceName, string speed, int volume)
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

                if (!string.IsNullOrWhiteSpace(voiceName))
                    synthesizer.SelectVoice(voiceName);

                synthesizer.Rate = SpeedToRate(speed);
                synthesizer.Volume = Math.Clamp(volume, 0, 100);
                synthesizer.SpeakAsyncCancelAll();
                synthesizer.SpeakAsync("هذا اختبار للصوت. سيتم نطق مبلغ الدين بهذه الإعدادات.");
            }
        }
        catch
        {
        }

        return Task.CompletedTask;
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

                ApplySettings(synthesizer, _settings);
                synthesizer.SpeakAsyncCancelAll();
                synthesizer.SpeakAsync($"تم تسجيل دين بمبلغ {amount} دينار عراقي");
            }
        }
        catch
        {
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

                var arabicVoices = synthesizer.GetInstalledVoices()
                    .Where(v =>
                        v.Enabled &&
                        v.VoiceInfo.Culture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(v => ScoreVoice(v.VoiceInfo))
                    .ToList();

                var configured = arabicVoices.FirstOrDefault(v =>
                    string.Equals(v.VoiceInfo.Name, _settings.VoiceName, StringComparison.OrdinalIgnoreCase));

                var selected = configured ?? arabicVoices.FirstOrDefault();
                if (selected is not null)
                {
                    synthesizer.SelectVoice(selected.VoiceInfo.Name);
                    _settings.VoiceName = selected.VoiceInfo.Name;
                }

                ApplySettings(synthesizer, _settings);
                _synthesizer = synthesizer;
                return _synthesizer;
            }
            catch
            {
                return null;
            }
        }
    }

    private static void ApplySettings(SpeechSynthesizer? synthesizer, SpeechSettings settings)
    {
        if (synthesizer is null) return;

        if (!string.IsNullOrWhiteSpace(settings.VoiceName))
        {
            try { synthesizer.SelectVoice(settings.VoiceName); } catch { }
        }

        synthesizer.Rate = SpeedToRate(settings.Speed);
        synthesizer.Volume = Math.Clamp(settings.Volume, 0, 100);
    }

    private static SpeechSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new SpeechSettings();

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<SpeechSettings>(json) ?? new SpeechSettings();
            settings.Speed = NormalizeSpeed(settings.Speed);
            settings.Volume = Math.Clamp(settings.Volume, 0, 100);
            return settings;
        }
        catch
        {
            return new SpeechSettings();
        }
    }

    private static string NormalizeSpeed(string? speed) =>
        speed switch
        {
            "بطيء" => "بطيء",
            "سريع" => "سريع",
            _ => "طبيعي"
        };

    private static int SpeedToRate(string? speed) =>
        NormalizeSpeed(speed) switch
        {
            "بطيء" => -2,
            "سريع" => 2,
            _ => 0
        };

    private static string BuildDisplayName(VoiceInfo voice)
    {
        var culture = voice.Culture?.DisplayName ?? "العربية";
        return $"{voice.Name} — {culture}";
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

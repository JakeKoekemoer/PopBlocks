using PopBlocks.Engine.Services;

namespace PopBlocks.Services;

/// <summary>
/// <see cref="ILocalSettings"/> backed by the MAUI Preferences API (Android SharedPreferences).
/// Data stays on the device; there is no cloud sync and no personal information is stored.
/// </summary>
public sealed class LocalSettingsService : ILocalSettings
{
    public int GetInt(string key, int defaultValue) => Preferences.Default.Get(key, defaultValue);
    public void SetInt(string key, int value) => Preferences.Default.Set(key, value);
    public bool GetBool(string key, bool defaultValue) => Preferences.Default.Get(key, defaultValue);
    public void SetBool(string key, bool value) => Preferences.Default.Set(key, value);
    public string GetString(string key, string defaultValue) => Preferences.Default.Get(key, defaultValue);
    public void SetString(string key, string value) => Preferences.Default.Set(key, value);
}

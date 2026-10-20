using OpenSol.Core;

namespace SolAndroid;

/// <summary>
/// Keeps the auth token in the Android Keystore (SecureStorage).
/// Older versions kept it in Preferences, encrypted with a key shared by every install.
/// </summary>
public static class TokenStore
{
    private const string Key = "AuthToken";

    public static async Task<string> GetAsync()
    {
        // Move the token saved by older versions into SecureStorage
        if (Preferences.ContainsKey(Key))
        {
            string legacy = SecurityUtils.Decrypt(Preferences.Get(Key, ""));
            await SetAsync(legacy);
            return legacy;
        }

        return await SecureStorage.Default.GetAsync(Key) ?? "";
    }

    public static async Task SetAsync(string? token)
    {
        if (string.IsNullOrEmpty(token))
            SecureStorage.Default.Remove(Key);
        else
            await SecureStorage.Default.SetAsync(Key, token);

        Preferences.Remove(Key);
    }
}

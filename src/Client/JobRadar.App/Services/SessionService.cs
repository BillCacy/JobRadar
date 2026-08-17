namespace JobRadar.App.Services;

/// <summary>
/// Persists the "logged-in" UserId across app launches via MAUI's Preferences API (backed by
/// NSUserDefaults on Apple platforms, SharedPreferences on Android, the registry on Windows).
/// Pairs with the simplified X-User-Id auth described in JobRadar.Users — there's no password
/// or token here, just a remembered device-local id.
/// </summary>
public sealed class SessionService
{
    private const string UserIdKey = "jobradar.user_id";

    public Guid? UserId
    {
        get
        {
            var raw = Preferences.Default.Get(UserIdKey, string.Empty);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
        set => Preferences.Default.Set(UserIdKey, value?.ToString() ?? string.Empty);
    }
}

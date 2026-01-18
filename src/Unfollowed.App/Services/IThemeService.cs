using Unfollowed.App.Settings;

namespace Unfollowed.App.Services;

public interface IThemeService
{
    ThemeMode Mode { get; }

    void ApplyTheme(ThemeMode mode);
}

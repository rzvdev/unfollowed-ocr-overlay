using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using Unfollowed.App.Settings;

namespace Unfollowed.App.Services;

public sealed class ThemeService : IThemeService
{
    private static readonly Uri LightThemeUri = new("Themes/Colors.Light.xaml", UriKind.Relative);
    private static readonly Uri DarkThemeUri = new("Themes/Colors.Dark.xaml", UriKind.Relative);
    private readonly ResourceDictionary _lightDictionary = new() { Source = LightThemeUri };
    private readonly ResourceDictionary _darkDictionary = new() { Source = DarkThemeUri };

    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    public void ApplyTheme(ThemeMode mode)
    {
        Mode = mode;
        var resolvedMode = mode == ThemeMode.System ? ResolveSystemTheme() : mode;
        var resources = Application.Current?.Resources;

        if (resources is null)
        {
            return;
        }

        var dictionaries = resources.MergedDictionaries;
        RemoveThemeDictionaries(dictionaries);

        var dictionary = resolvedMode == ThemeMode.Dark ? _darkDictionary : _lightDictionary;
        dictionaries.Insert(0, dictionary);
    }

    private static void RemoveThemeDictionaries(Collection<ResourceDictionary> dictionaries)
    {
        var toRemove = dictionaries
            .Where(dictionary => dictionary.Source == LightThemeUri || dictionary.Source == DarkThemeUri)
            .ToList();

        foreach (var dictionary in toRemove)
        {
            dictionaries.Remove(dictionary);
        }
    }

    private static ThemeMode ResolveSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int useLight)
            {
                return useLight > 0 ? ThemeMode.Light : ThemeMode.Dark;
            }
        }
        catch
        {
        }

        return ThemeMode.Light;
    }
}

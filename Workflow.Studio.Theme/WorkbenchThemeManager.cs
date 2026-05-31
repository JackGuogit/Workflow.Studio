using System.Windows;

namespace Workflow.Studio.Theme;

public enum WorkbenchThemeKind
{
    Light,
    Dark
}

public static class WorkbenchThemeManager
{
    private static readonly IReadOnlyDictionary<WorkbenchThemeKind, Uri> ThemeUris =
        new Dictionary<WorkbenchThemeKind, Uri>
        {
            [WorkbenchThemeKind.Light] = new("pack://application:,,,/Workflow.Studio.Theme;component/Themes/Light.xaml", UriKind.Absolute),
            [WorkbenchThemeKind.Dark] = new("pack://application:,,,/Workflow.Studio.Theme;component/Themes/Dark.xaml", UriKind.Absolute)
        };

    private static readonly Dictionary<WorkbenchThemeKind, ResourceDictionary> LoadedThemes = [];
    private static bool _isInitialized;

    public static event EventHandler? ThemeChanged;

    public static WorkbenchThemeKind ActiveTheme { get; private set; } = WorkbenchThemeKind.Light;

    public static string ActiveThemeDisplayName => ActiveTheme == WorkbenchThemeKind.Dark ? "深色" : "浅色";

    public static void EnsureInitialized()
    {
        if (_isInitialized || Application.Current is null)
        {
            return;
        }

        var existingTheme = ThemeUris.FirstOrDefault(pair =>
            Application.Current.Resources.MergedDictionaries.Any(dictionary => IsSameUri(dictionary.Source, pair.Value)));

        if (!EqualityComparer<KeyValuePair<WorkbenchThemeKind, Uri>>.Default.Equals(existingTheme, default))
        {
            ActiveTheme = existingTheme.Key;
        }
        else
        {
            ApplyTheme(WorkbenchThemeKind.Light);
        }

        _isInitialized = true;
    }

    public static void SetNextTheme()
    {
        EnsureInitialized();
        SetTheme(ActiveTheme == WorkbenchThemeKind.Light ? WorkbenchThemeKind.Dark : WorkbenchThemeKind.Light);
    }

    public static void SetTheme(WorkbenchThemeKind theme)
    {
        EnsureInitialized();

        if (Application.Current is null)
        {
            return;
        }

        ApplyTheme(theme);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void ApplyTheme(WorkbenchThemeKind theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

        for (var index = mergedDictionaries.Count - 1; index >= 0; index--)
        {
            if (IsThemeDictionary(mergedDictionaries[index]))
            {
                mergedDictionaries.RemoveAt(index);
            }
        }

        if (!LoadedThemes.TryGetValue(theme, out var targetTheme))
        {
            targetTheme = new ResourceDictionary
            {
                Source = ThemeUris[theme]
            };

            LoadedThemes[theme] = targetTheme;
        }

        mergedDictionaries.Add(targetTheme);
        ActiveTheme = theme;
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        return dictionary.Source is not null && ThemeUris.Values.Any(themeUri => IsSameUri(dictionary.Source, themeUri));
    }

    private static bool IsSameUri(Uri? left, Uri? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(left.OriginalString, right.OriginalString, StringComparison.OrdinalIgnoreCase);
    }
}

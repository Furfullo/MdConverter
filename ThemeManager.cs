using System.IO;
using System.Windows;

namespace MdConverter;

public static class ThemeManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MdConverter", "theme.txt");

    public static string Current { get; private set; } = "light";

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                Current = File.ReadAllText(SettingsPath).Trim().ToLower() == "dark" ? "dark" : "light";
        }
        catch { /* default to light on any error */ }
    }

    public static void Apply(string theme)
    {
        Current = theme == "dark" ? "dark" : "light";

        var uri  = new Uri($"Themes/{(Current == "dark" ? "Dark" : "Light")}.xaml", UriKind.Relative);
        var dict = new ResourceDictionary { Source = uri };

        var merged   = Application.Current.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("Themes/") == true);

        if (existing is not null) merged.Remove(existing);
        merged.Add(dict);
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, Current);
        }
        catch { /* ignore save errors */ }
    }
}

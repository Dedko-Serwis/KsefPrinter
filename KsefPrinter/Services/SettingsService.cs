using System.IO;
using System.Text.Json;
using KsefPrinter.Models;

namespace KsefPrinter.Services;

public class SettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KsefPrinter");

    private readonly string _file = Path.Combine(SettingsDirectory, "settings.json");
    private readonly string _legacyFile = Path.Combine(AppContext.BaseDirectory, "settings.json");

    public AppSettings Load()
    {
        // Dotychczasowe ustawienia przenosimy tylko raz. Jeśli nowy plik już istnieje,
        // nie nadpisujemy go starszą kopią z katalogu programu.
        if (!File.Exists(_file) && File.Exists(_legacyFile))
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.Copy(_legacyFile, _file, overwrite: false);
        }

        if (!File.Exists(_file)) return new AppSettings();

        var json = File.ReadAllText(_file);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_file, json);
    }
}

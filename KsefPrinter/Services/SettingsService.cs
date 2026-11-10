using System.Text.Json;
using System.IO;
using KsefPrinter.Models;

namespace KsefPrinter.Services;

public class SettingsService
{
    private readonly string _file =
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(_file)) return new AppSettings();

        var json = File.ReadAllText(_file);

        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, 
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_file, json);
    }
}
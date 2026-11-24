using System.IO;
using System.Text.Json;
using KsefPrinter.Models;

namespace KsefPrinter.Services;

public class SettingsService
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    internal static readonly string DataDirectory = Path.Combine(LocalAppData, "Dedko Serwis", "KsefPrinter");

    private readonly string _file = Path.Combine(DataDirectory, "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(_file))
            ImportLegacySettings();

        if (!File.Exists(_file)) return new AppSettings();

        var json = File.ReadAllText(_file);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    private void ImportLegacySettings()
    {
        string source = Path.Combine(LocalAppData, "KsefPrinter", "settings.json");
        if (!File.Exists(source))
            source = Path.Combine(AppContext.BaseDirectory, "settings.json");
        if (!File.Exists(source))
            return;

        Directory.CreateDirectory(DataDirectory);
        string temporary = Path.Combine(DataDirectory, $".settings-{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(source, temporary);
            File.Move(temporary, _file, overwrite: false);
        }
        catch (IOException) when (File.Exists(_file))
        {
            // Zachowaj ustawienia zapisane równolegle w nowym katalogu.
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DataDirectory);
        var json = JsonSerializer.Serialize(settings,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_file, json);
    }
}

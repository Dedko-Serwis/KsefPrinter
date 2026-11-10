using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using KsefPrinter.Infrastructure;
using KsefPrinter.Services;

namespace KsefPrinter.ViewModels;

public sealed class UpdateViewModel : ObservableObject
{
    private const string ApiUrl = "https://api.github.com/repos/Dedko-Serwis/KsefPrinter/releases/latest";
    private static readonly HttpClient Client = CreateClient();
    private readonly SettingsService _settings = new();
    private string _status = "Nie sprawdzono aktualizacji.";
    private string _notes = "";
    private bool _hasUpdate;
    private bool _busy;
    private string _latestVersion = "—";
    private DateTimeOffset? _lastCheck;
    private DateTimeOffset? _lastUpdated;
    public string CurrentVersion => (Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0)).ToString(3);
    public string LatestVersion { get => _latestVersion; private set => SetProperty(ref _latestVersion, value); }
    public string LastCheckText => _lastCheck?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "Nigdy";
    public string LastUpdatedText => _lastUpdated?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "Nigdy";
    public string[] UpdateCheckOptions { get; } = ["Nie sprawdzaj", "Co 30 dni", "Co 7 dni"];
    private string _selectedUpdateCheck = "Co 7 dni";
    public string SelectedUpdateCheck
    {
        get => _selectedUpdateCheck;
        set
        {
            if (!SetProperty(ref _selectedUpdateCheck, value)) return;
            var settings = _settings.Load();
            settings.UpdateCheckDays = value switch { "Nie sprawdzaj" => 0, "Co 30 dni" => 30, _ => 7 };
            try { _settings.Save(settings); }
            catch (Exception ex) { UpdateStatus = "Błąd zapisu ustawień: " + ex.Message; }
        }
    }
    private string? _assetUrl;
    private string? _assetName;
    private string? _assetDigest;
    public string UpdateStatus { get => _status; private set => SetProperty(ref _status, value); }
    public string ReleaseNotes { get => _notes; private set => SetProperty(ref _notes, value); }
    public bool HasUpdate { get => _hasUpdate; private set { SetProperty(ref _hasUpdate, value); OnPropertyChanged(nameof(TabColor)); } }
    public Brush TabColor => HasUpdate ? Brushes.Red : Brushes.Black;
    public ICommand CheckCommand { get; }
    public ICommand InstallCommand { get; }

    public UpdateViewModel()
    {
        CheckCommand = new RelayCommand(async _ => await CheckAsync());
        InstallCommand = new RelayCommand(async _ => await InstallAsync());
        var settings = _settings.Load();
        _lastCheck = settings.LastUpdateCheckUtc;
        _lastUpdated = settings.LastUpdatedUtc;
        _selectedUpdateCheck = settings.UpdateCheckDays switch { 0 => "Nie sprawdzaj", 30 => "Co 30 dni", _ => "Co 7 dni" };
        string version = CurrentVersion;
        if (settings.LastRunVersion is not null &&
            Version.TryParse(settings.LastRunVersion, out var previous) &&
            Version.TryParse(version, out var currentVersion) && currentVersion > previous)
        {
            settings.LastUpdatedUtc = DateTimeOffset.UtcNow;
            _lastUpdated = settings.LastUpdatedUtc;
            OnPropertyChanged(nameof(LastUpdatedText));
        }
        if (settings.LastRunVersion != version)
        {
            settings.LastRunVersion = version;
            _settings.Save(settings);
        }
        _ = CheckOnStartupAsync();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("KsefPrinter", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private async Task CheckOnStartupAsync()
    {
        await Task.Yield();
        var settings = _settings.Load();
        if (settings.UpdateCheckDays == 0) return;
        if (settings.LastUpdateCheckUtc is { } last &&
            DateTimeOffset.UtcNow - last < TimeSpan.FromDays(settings.UpdateCheckDays)) return;
        await CheckAsync();
    }

    private async Task CheckAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            UpdateStatus = "Sprawdzam GitHub Releases...";
            using var response = await Client.GetAsync(ApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                UpdateStatus = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "Brak publicznego wydania GitHub Releases (repozytorium może być prywatne)."
                    : $"Błąd sprawdzania: HTTP {(int)response.StatusCode}.";
                return;
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var release = json.RootElement;
            var tag = release.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
            if (!Version.TryParse(tag, out var latest)) throw new FormatException("Nieprawidłowy numer wersji Release.");
            var current = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);
            var newer = latest > current;
            LatestVersion = latest.ToString(3);
            ReleaseNotes = release.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
            _assetUrl = _assetName = _assetDigest = null;
            if (newer && release.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    if (name is null || !name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase)) continue;
                    _assetUrl = asset.GetProperty("browser_download_url").GetString();
                    _assetName = name;
                    _assetDigest = asset.TryGetProperty("digest", out var digest) ? digest.GetString() : null;
                    break;
                }
            }
            HasUpdate = newer && _assetUrl is not null && _assetDigest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true;
            UpdateStatus = newer
                ? HasUpdate ? $"Nowa wersja: {latest} (zainstalowana: {current})." : $"Dostępna wersja {latest}, ale brakuje instalatora *-Setup.exe z sumą SHA-256."
                : $"Masz aktualną wersję: {current}.";
            var settings = _settings.Load();
            settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            _settings.Save(settings);
            _lastCheck = settings.LastUpdateCheckUtc;
            OnPropertyChanged(nameof(LastCheckText));
        }
        catch (Exception ex)
        {
            UpdateStatus = "Nie udało się sprawdzić aktualizacji: " + ex.Message;
        }
        finally { _busy = false; }
    }

    private async Task InstallAsync()
    {
        if (_busy || !HasUpdate || _assetUrl is null || _assetName is null || _assetDigest is null) return;
        if (MessageBox.Show("Pobrać instalator i uruchomić aktualizację? Zapisz wcześniej swoją pracę.", "KsefPrinter",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _busy = true;
        string? file = null;
        try
        {
            UpdateStatus = "Pobieram aktualizację...";
            var directory = Path.Combine(Path.GetTempPath(), "KsefPrinter-Updates");
            Directory.CreateDirectory(directory);
            file = Path.Combine(directory, Guid.NewGuid().ToString("N") + "-Setup.exe");
            using var response = await Client.GetAsync(_assetUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync())
            await using (var output = File.Create(file))
                await input.CopyToAsync(output);
            await using var stream = File.OpenRead(file);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            if (!hash.Equals(_assetDigest["sha256:".Length..], StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Suma SHA-256 instalatora nie zgadza się z GitHub Release.");
            UpdateStatus = "Pobrano i zweryfikowano. Uruchamiam instalator...";
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateStatus = "Błąd aktualizacji: " + ex.Message;
            if (file is not null) try { File.Delete(file); } catch (IOException) { }
        }
        finally { _busy = false; }
    }
}

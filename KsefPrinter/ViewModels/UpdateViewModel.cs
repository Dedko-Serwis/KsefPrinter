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
